namespace SmartParking.PaymentService.Application.Common.Interfaces;

using SmartParking.PaymentService.Application.DTOs.Invoices;

public interface IPdfInvoiceGenerator
{
    /// <summary>
    /// Generates authoritative server-side PDF invoice document bytes from the customer invoice DTO.
    /// </summary>
    /// <param name="invoice">Authoritative invoice DTO containing session, pricing, and financial details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rendered PDF document as raw byte array.</returns>
    Task<byte[]> GenerateInvoicePdfAsync(CustomerInvoiceResponseDto invoice, CancellationToken cancellationToken = default);
}
