namespace MakeUpServiceApi.Interface
{
    public interface ITravelFeeService
    {
        Task<(decimal TotalFee, decimal DistanceKm, decimal DistanceFee)> CalculateFeeAsync(int? areaID, string clientAddress);
    }
}
