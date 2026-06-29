using E6CarSpa.Contracts;
using E6CarSpa.Domain.Entities;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Domain.Services;
using E6CarSpa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace E6CarSpa.Api.Services;

/// <summary>
/// Orchestrates the billing workflow: intake → quotation → invoice → payment,
/// including GST recalculation, sequential invoice numbering, inventory consumption
/// and the post-payment WhatsApp notification.
/// </summary>
public class InvoiceService(AppDbContext db, WhatsAppService whatsApp)
{
    private static readonly Func<IQueryable<Invoice>, IQueryable<Invoice>> WithGraph =
        q => q.Include(i => i.Customer)
              .Include(i => i.Vehicle)
              .Include(i => i.Items)
              .Include(i => i.Payments);

    // ---------- Steps 1-3: create the quotation ----------

    public async Task<Invoice> CreateQuotationAsync(CreateQuotationRequest req, Guid? userId)
    {
        var (customer, vehicle) = await ResolveCustomerVehicleAsync(req.CustomerName, req.CustomerPhone, req.CarNumber, req.CarModel);

        var invoice = new Invoice
        {
            Customer = customer,
            Vehicle = vehicle,
            Status = InvoiceStatus.Quotation,
            DiscountAmount = req.DiscountAmount,
            Notes = req.Notes,
            IsGstApplicable = req.ApplyGst,
            CreatedByUserId = userId
        };

        await AddItemsAsync(invoice, req.Items);
        GstCalculator.Recalculate(invoice, interState: false);

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return await GetEntityAsync(invoice.Id) ?? invoice;
    }

    // ---------- Step 4: edit a quotation/invoice ----------

    public async Task<Invoice?> UpdateAsync(Guid id, UpdateInvoiceRequest req)
    {
        var invoice = await GetEntityAsync(id);
        if (invoice is null) return null;
        if (invoice.Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled)
            throw new InvalidOperationException("A paid or cancelled invoice cannot be edited.");

        db.InvoiceItems.RemoveRange(invoice.Items);
        invoice.Items.Clear();
        invoice.DiscountAmount = req.DiscountAmount;
        invoice.Notes = req.Notes;
        invoice.IsGstApplicable = req.ApplyGst;
        await AddItemsAsync(invoice, req.Items);
        GstCalculator.Recalculate(invoice, interState: false);
        invoice.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return invoice;
    }

    /// <summary>
    /// Finalise: assign a permanent invoice number (sequential, never resets), move the
    /// status to Invoiced, and consume inventory per each service's bill-of-materials.
    /// </summary>
    public async Task<Invoice?> FinaliseAsync(Guid id, Guid? userId)
    {
        var invoice = await GetEntityAsync(id);
        if (invoice is null) return null;
        if (invoice.Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled)
            throw new InvalidOperationException("Invoice is already closed.");
        if (!string.IsNullOrEmpty(invoice.InvoiceNumber))
            return invoice; // already finalised

        await using var tx = await db.Database.BeginTransactionAsync();

        var settings = await db.CompanySettings.FirstAsync();
        var next = settings.LastInvoiceSequence + 1;
        settings.LastInvoiceSequence = next;
        invoice.SequenceNumber = next;
        invoice.InvoiceNumber = $"{settings.InvoicePrefix}{next}";
        invoice.Status = InvoiceStatus.Invoiced;

        // await ConsumeInventoryAsync(invoice, userId); // Disabled per user request

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return invoice;
    }

    // ---------- Step 5: record payment + WhatsApp ----------

    public async Task<Invoice?> RecordPaymentAsync(Guid id, RecordPaymentRequest req, Guid? userId)
    {
        var invoice = await GetEntityAsync(id);
        if (invoice is null) return null;
        if (invoice.Status == InvoiceStatus.Cancelled)
            throw new InvalidOperationException("Cannot pay a cancelled invoice.");

        // Auto-finalise if a payment is taken directly against a quotation.
        if (string.IsNullOrEmpty(invoice.InvoiceNumber))
        {
            await FinaliseAsync(id, userId);
            invoice = await GetEntityAsync(id) ?? invoice;
        }

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            Method = req.Method,
            Amount = req.Amount,
            Reference = req.Reference,
            ReceivedByUserId = userId,
            PaidAt = DateTime.UtcNow
        };
        invoice.Payments.Add(payment);
        // Explicit Add so EF inserts the payment (the invoice is already tracked).
        db.Payments.Add(payment);
        invoice.AmountPaid = invoice.Payments.Sum(p => p.Amount);

        var nowFullyPaid = invoice.Balance <= 0 && invoice.Status != InvoiceStatus.Paid;
        if (nowFullyPaid)
        {
            invoice.Status = InvoiceStatus.Paid;
            invoice.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        // Fire the automatic thank-you only once, when the bill is fully settled.
        if (nowFullyPaid)
            await whatsApp.SendPaymentThankYouAsync(invoice);

        return invoice;
    }

    public async Task<Invoice?> CancelAsync(Guid id)
    {
        var invoice = await GetEntityAsync(id);
        if (invoice is null) return null;
        if (invoice.Status == InvoiceStatus.Paid)
            throw new InvalidOperationException("A paid invoice cannot be cancelled.");
        invoice.Status = InvoiceStatus.Cancelled;
        await db.SaveChangesAsync();
        return invoice;
    }

    // ---------- Queries ----------

    public Task<Invoice?> GetEntityAsync(Guid id) =>
        WithGraph(db.Invoices).FirstOrDefaultAsync(i => i.Id == id);

    public async Task<List<Invoice>> ListAsync(InvoiceStatus? status, string? search)
    {
        var q = db.Invoices.AsNoTracking().Include(i => i.Customer).Include(i => i.Vehicle).AsQueryable();
        if (status.HasValue) q = q.Where(i => i.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(i =>
                i.Customer!.Name.ToLower().Contains(s) ||
                i.Customer.Phone.Contains(s) ||
                i.Vehicle!.CarNumber.ToLower().Contains(s) ||
                (i.InvoiceNumber != null && i.InvoiceNumber.ToLower().Contains(s)));
        }
        return await q.OrderByDescending(i => i.CreatedAt).Take(500).ToListAsync();
    }

    // ---------- Helpers ----------

    private async Task<(Customer customer, Vehicle vehicle)> ResolveCustomerVehicleAsync(
        string name, string phone, string carNumber, string carModel)
    {
        phone = phone.Trim();
        var car = carNumber.Trim().ToUpperInvariant().Replace(" ", "");

        var customer = await db.Customers.Include(c => c.Vehicles)
            .FirstOrDefaultAsync(c => c.Phone == phone);

        if (customer is null)
        {
            customer = new Customer { Name = name.Trim(), Phone = phone };
            db.Customers.Add(customer);
        }
        else if (!string.IsNullOrWhiteSpace(name) && customer.Name != name.Trim())
        {
            customer.Name = name.Trim(); // keep the latest spelling
        }

        var vehicle = customer.Vehicles.FirstOrDefault(v => v.CarNumber == car);
        if (vehicle is null)
        {
            vehicle = new Vehicle { CarNumber = car, CarModel = carModel.Trim(), CustomerId = customer.Id, Customer = customer };
            customer.Vehicles.Add(vehicle);
            // Explicit Add so EF inserts (not updates) when the customer is already tracked.
            db.Vehicles.Add(vehicle);
        }
        else if (!string.IsNullOrWhiteSpace(carModel) && vehicle.CarModel != carModel.Trim())
        {
            vehicle.CarModel = carModel.Trim();
        }

        return (customer, vehicle);
    }

    private async Task AddItemsAsync(Invoice invoice, List<InvoiceItemInput> inputs)
    {
        var settings = await db.CompanySettings.FirstAsync();

        foreach (var input in inputs)
        {
            string description;
            string hsn;
            decimal gstRate;
            decimal unitPrice;

            if (input.ServiceId is Guid sid)
            {
                var svc = await db.Services.FindAsync(sid)
                          ?? throw new InvalidOperationException($"Service {sid} not found.");
                description = svc.Name;
                hsn = svc.HsnSac;
                gstRate = svc.GstRate;
                unitPrice = input.UnitPriceOverride ?? svc.DefaultPrice;
            }
            else if (input.ProductId is Guid pid)
            {
                var prod = await db.Products.FindAsync(pid)
                           ?? throw new InvalidOperationException($"Product {pid} not found.");
                description = prod.Name;
                hsn = prod.HsnSac;
                gstRate = prod.GstRate;
                unitPrice = input.UnitPriceOverride ?? prod.UnitCost;
            }
            else
            {
                description = input.Description ?? "Item";
                hsn = "";
                gstRate = settings.DefaultGstRate;
                unitPrice = input.UnitPriceOverride ?? 0m;
            }

            var item = new InvoiceItem
            {
                InvoiceId = invoice.Id,
                ServiceId = input.ServiceId,
                ProductId = input.ProductId,
                Description = description,
                HsnSac = hsn,
                GstRate = gstRate,
                Quantity = input.Quantity <= 0 ? 1m : input.Quantity,
                UnitPrice = unitPrice,
                DiscountAmount = input.DiscountAmount
            };
            invoice.Items.Add(item);
            // Explicit Add so new lines INSERT even when the invoice is already tracked (edit flow).
            db.InvoiceItems.Add(item);
        }
    }

    /// <summary>Deduct stock for every service line based on its bill-of-materials.</summary>
    private async Task ConsumeInventoryAsync(Invoice invoice, Guid? userId)
    {
        var serviceIds = invoice.Items.Where(i => i.ServiceId.HasValue).Select(i => i.ServiceId!.Value).ToList();
        if (serviceIds.Count == 0) return;

        var boms = await db.ServiceProducts
            .Where(sp => serviceIds.Contains(sp.ServiceId))
            .Include(sp => sp.Product)
            .ToListAsync();

        foreach (var item in invoice.Items.Where(i => i.ServiceId.HasValue))
        {
            foreach (var bom in boms.Where(b => b.ServiceId == item.ServiceId!.Value))
            {
                if (bom.Product is null) continue;
                var qty = bom.DefaultQuantity * item.Quantity;
                bom.Product.StockQuantity -= qty;

                db.StockMovements.Add(new StockMovement
                {
                    ProductId = bom.ProductId,
                    Type = StockMovementType.Consumption,
                    Quantity = -qty,
                    UnitCost = bom.Product.UnitCost,
                    InvoiceId = invoice.Id,
                    Reference = invoice.InvoiceNumber,
                    Note = $"Auto-consumed for {item.Description}",
                    CreatedByUserId = userId
                });
            }
        }
    }
}
