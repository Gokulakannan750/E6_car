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
            var service = new InvoiceService(db, whatsapp);

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
            var service = new InvoiceService(db, whatsapp);

            var req = new CreateQuotationRequest(
                CustomerName: "Jane Doe",
                CustomerPhone: "9988776655",
                CarNumber: "KA01 XY 9999",
                CarModel: "Toyota Fortuner",
                Items: new List<InvoiceItemInput>(),
                DiscountAmount: 0m,
                Notes: "",
                ApplyGst: false
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
    }
}
