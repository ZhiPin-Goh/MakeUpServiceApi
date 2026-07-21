namespace MakeUpServiceApi.Interface
{
    public interface ITravelFeeService
    {
        Task<(decimal TotalFee, decimal DistanceKm)> CalculateFeeAsync(int? areaID, string clientAddress);
    }
}
