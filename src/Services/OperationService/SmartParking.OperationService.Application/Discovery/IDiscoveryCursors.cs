namespace SmartParking.OperationService.Application.Discovery;

public interface IDiscoveryCursors
{
    string Encode(DiscoveryCursor cursor);
    DiscoveryCursor Decode(string cursor);
}
