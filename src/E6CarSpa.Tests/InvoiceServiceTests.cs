using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using E6CarSpa.Api.Config;
using E6CarSpa.Api.Services;
using E6CarSpa.Contracts;
using E6CarSpa.Domain.Entities;
using E6CarSpa.Domain.Enums;
using E6CarSpa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace E6CarSpa.Tests
{
    public class InvoiceServiceTests
    {
        private AppDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var db = new AppDbContext(options);
            
            // Seed required company settings for testing
            db.CompanySettings.Add(new CompanySettings
            {
                Name = "Test Company",
                DefaultGstRate = 18m,
                InvoicePrefix = "TEST/"
            });
            db.SaveChanges();

            return db;
        }

        private WhatsAppService CreateWhatsAppService(AppDbContext db)
        {
            var mockFactory = new Mock<IHttpClientFactory>();
            var mockOptions = new Mock<IOptions<WhatsAppOptions>>();
            mockOptions.Setup(o => o.Value).Returns(new WhatsAppOptions { Enabled = false });
            var mockLogger = new Mock<ILogger<WhatsAppService>>();

            return new WhatsAppService(mockFactory.Object, mockOptions.Object, db, mockLogger.Object);
        }

        [Fact]
        public async Task CreateQuotationAsync_ShouldCreateNewCustomerAndVehicle()
        {
            // Arrange
            using var db = CreateDbContext();
            var whatsapp = CreateWhatsAppService(db);
            var service = new InvoiceService(db, whatsapp, new PdfInvoiceService(db));

            var req = new CreateQuotationRequest(
                CustomerName: "John Doe",
                CustomerPhone: "9876543210",
                CarNumber: "TN33 AB 1234",
                CarModel: "Honda City",
                Items: new List<InvoiceItemInput>
                {
                    new InvoiceItemInput(null, null, "Custom Service", 1, 1000m, 0m)
                },
                DiscountAmount: 0m,
                Notes: "Test Notes",
                ApplyGst: true
            );

            // Act
            var quotation = await service.CreateQuotationAsync(req, Guid.NewGuid());

            // Assert
            Assert.NotNull(quotation);
            Assert.Equal("John Doe", quotation.Customer?.Name);
            Assert.Equal("9876543210", quotation.Customer?.Phone);
            Assert.Equal("TN33AB1234", quotation.Vehicle?.CarNumber); // Note: Service strips spaces
            Assert.Equal("Honda City", quotation.Vehicle?.CarModel);
            Assert.Equal(InvoiceStatus.Quotation, quotation.Status);
            
            // GST Math check (1000 + 18% = 1180)
            Assert.Equal(1000m, quotation.SubTotal);
            Assert.Equal(180m, quotation.TotalTax);
            Assert.Equal(1180m, quotation.GrandTotal);
        }

        [Fact]
        public async Task FinaliseAsync_ShouldAssignInvoiceNumber()
        {
            // Arrange
            using var db = CreateDbContext();
            var whatsapp = CreateWhatsAppService(db);
            var service = new InvoiceService(db, whatsapp, new PdfInvoiceService(db));

            var req = new CreateQuotationRequest(
                CustomerName: "Jane Doe",
                CustomerPhone: "9988776655",
                CarNumber: "KA01 XY 9999",
                CarModel: "Toyota Fortuner",
                Items: new List<InvoiceItemInput>(),
                DiscountAmount: 0m,
                Notes: "",
                ApplyGst: true
            );

            var quotation = await service.CreateQuotationAsync(req, Guid.NewGuid());
            Assert.Null(quotation.InvoiceNumber); // Starts empty

            // Act
            var finalised = await service.FinaliseAsync(quotation.Id, Guid.NewGuid());

            // Assert
            Assert.NotNull(finalised);
            Assert.NotNull(finalised.InvoiceNumber);
            Assert.StartsWith("TEST/", finalised.InvoiceNumber);
            Assert.Equal(InvoiceStatus.Invoiced, finalised.Status);
        }

        [Fact]
        public async Task Finalise_NonGstAndGst_UseSeparateSeries()
        {
            using var db = CreateDbContext();
            var svc = NewService(db);

            // Non-GST bills get their own {Prefix}{Year}/0000 series (default prefix "E6/").
            var nonGst = await svc.FinaliseAsync(
                (await svc.CreateQuotationAsync(Quote(phone: "9000000011", car: "TN09 N 1", gst: false), null)).Id, null);
            Assert.StartsWith("E6/", nonGst!.InvoiceNumber);
            Assert.EndsWith("/0001", nonGst.InvoiceNumber);

            // A GST bill keeps running on the GST series, unaffected by the non-GST one.
            var gst = await svc.FinaliseAsync(
                (await svc.CreateQuotationAsync(Quote(phone: "9000000012", car: "TN09 G 1", gst: true), null)).Id, null);
            Assert.Equal("TEST/1", gst!.InvoiceNumber);

            // ...and the next non-GST bill continues its own count.
            var nonGst2 = await svc.FinaliseAsync(
                (await svc.CreateQuotationAsync(Quote(phone: "9000000013", car: "TN09 N 2", gst: false), null)).Id, null);
            Assert.EndsWith("/0002", nonGst2!.InvoiceNumber);
        }

        // ---------- helpers ----------

        private static CreateQuotationRequest Quote(
            string phone = "9000000001", string car = "TN01 AA 0001", decimal price = 1000m, bool gst = true) =>
            new(
                CustomerName: "Cust", CustomerPhone: phone, CarNumber: car, CarModel: "Model",
                DiscountAmount: 0m, Notes: null,
                Items: new List<InvoiceItemInput> { new(null, null, "Service", 1, price, 0m) },
                ApplyGst: gst);

        private InvoiceService NewService(AppDbContext db) => new(db, CreateWhatsAppService(db), new PdfInvoiceService(db));

        // ---------- numbering ----------

        [Fact]
        public async Task Finalise_AssignsSequentialNumbers_NoReset()
        {
            using var db = CreateDbContext();
            var svc = NewService(db);

            var a = await svc.FinaliseAsync((await svc.CreateQuotationAsync(Quote(phone: "9000000001", car: "TN01 A 1"), null)).Id, null);
            var b = await svc.FinaliseAsync((await svc.CreateQuotationAsync(Quote(phone: "9000000002", car: "TN01 A 2"), null)).Id, null);
            var c = await svc.FinaliseAsync((await svc.CreateQuotationAsync(Quote(phone: "9000000003", car: "TN01 A 3"), null)).Id, null);

            Assert.Equal("TEST/1", a!.InvoiceNumber);
            Assert.Equal("TEST/2", b!.InvoiceNumber);
            Assert.Equal("TEST/3", c!.InvoiceNumber);
        }

        [Fact]
        public async Task Finalise_IsIdempotent()
        {
            using var db = CreateDbContext();
            var svc = NewService(db);
            var q = await svc.CreateQuotationAsync(Quote(), null);

            var first = await svc.FinaliseAsync(q.Id, null);
            var second = await svc.FinaliseAsync(q.Id, null);

            Assert.Equal(first!.InvoiceNumber, second!.InvoiceNumber);
        }

        // ---------- editing rules ----------

        [Fact]
        public async Task Update_OnPaidInvoice_Throws()
        {
            using var db = CreateDbContext();
            var svc = NewService(db);
            var q = await svc.CreateQuotationAsync(Quote(price: 500m), null);
            await svc.RecordPaymentAsync(q.Id, new RecordPaymentRequest(PaymentMethod.Cash, 590m, null), null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.UpdateAsync(q.Id, new UpdateInvoiceRequest(0m, null, new(), true)));
        }

        [Fact]
        public async Task Cancel_PaidInvoice_Throws()
        {
            using var db = CreateDbContext();
            var svc = NewService(db);
            var q = await svc.CreateQuotationAsync(Quote(price: 500m), null);
            await svc.RecordPaymentAsync(q.Id, new RecordPaymentRequest(PaymentMethod.Cash, 590m, null), null);

            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CancelAsync(q.Id));
        }

        // ---------- payments ----------

        [Fact]
        public async Task RecordPayment_Partial_KeepsInvoicedWithBalance()
        {
            using var db = CreateDbContext();
            var svc = NewService(db);
            var q = await svc.CreateQuotationAsync(Quote(price: 1000m), null); // grand 1180

            var after = await svc.RecordPaymentAsync(q.Id, new RecordPaymentRequest(PaymentMethod.Cash, 500m, null), null);

            Assert.Equal(InvoiceStatus.Invoiced, after!.Status);
            Assert.Equal(680m, after.Balance);
            Assert.Null(after.CompletedAt);
        }

        [Fact]
        public async Task RecordPayment_InFull_MarksPaidAndSetsCompletedAt()
        {
            using var db = CreateDbContext();
            var svc = NewService(db);
            var q = await svc.CreateQuotationAsync(Quote(price: 1000m), null); // grand 1180

            var after = await svc.RecordPaymentAsync(q.Id, new RecordPaymentRequest(PaymentMethod.Upi, 1180m, null), null);

            Assert.Equal(InvoiceStatus.Paid, after!.Status);
            Assert.Equal(0m, after.Balance);
            Assert.NotNull(after.CompletedAt);
        }

        [Fact]
        public async Task RecordPayment_OnQuotation_AutoFinalises()
        {
            using var db = CreateDbContext();
            var svc = NewService(db);
            var q = await svc.CreateQuotationAsync(Quote(price: 1000m), null);
            Assert.Null(q.InvoiceNumber);

            var after = await svc.RecordPaymentAsync(q.Id, new RecordPaymentRequest(PaymentMethod.Card, 100m, null), null);

            Assert.NotNull(after!.InvoiceNumber); // a number was assigned on the way in
        }

        // ---------- customer / vehicle resolution ----------

        [Fact]
        public async Task CreateQuotation_ReusesExistingCustomerByPhone()
        {
            using var db = CreateDbContext();
            var svc = NewService(db);

            await svc.CreateQuotationAsync(Quote(phone: "9111111111", car: "TN09 X 1"), null);
            await svc.CreateQuotationAsync(Quote(phone: "9111111111", car: "TN09 X 2"), null);

            Assert.Equal(1, await db.Customers.CountAsync(c => c.Phone == "9111111111"));
        }

        [Fact]
        public async Task CreateQuotation_NormalisesCarNumber()
        {
            using var db = CreateDbContext();
            var svc = NewService(db);

            var q = await svc.CreateQuotationAsync(Quote(phone: "9222222222", car: "tn 33 ab 1234"), null);

            Assert.Equal("TN33AB1234", q.Vehicle?.CarNumber);
        }

        [Fact]
        public async Task NonGstQuotation_HasZeroTax()
        {
            using var db = CreateDbContext();
            var svc = NewService(db);

            var q = await svc.CreateQuotationAsync(Quote(price: 1000m, gst: false), null);

            Assert.Equal(0m, q.TotalTax);
            Assert.Equal(1000m, q.GrandTotal);
        }
    }
}
