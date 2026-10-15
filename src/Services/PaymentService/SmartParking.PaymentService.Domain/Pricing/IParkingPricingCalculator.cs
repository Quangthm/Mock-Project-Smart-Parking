namespace SmartParking.PaymentService.Domain.Pricing;

public interface IParkingPricingCalculator
{
    PricingCalculationResult Calculate(PricingCalculationRequest request);
}
