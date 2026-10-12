using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Npgsql;
using SmartParking.ParkingService.Persistence;

namespace SmartParking.ParkingService.Tests;

public sealed class GateAuthenticationTests
{
    [Fact]
    public async Task GatePoolPreservesConfiguredPasswordDuringAuthentication()
    {
        // A tiny local protocol peer requests the configured password then rejects
        // the connection. This tests credential forwarding without an installed
        // PostgreSQL server or any real application credentials.
        const string dummyPassword = "only-for-this-protocol-test";
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;

        var peer = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(deadline.Token);
            await using var stream = socket.GetStream();
            var length = new byte[4];
            await stream.ReadExactlyAsync(length, deadline.Token);
            var startupLength = BinaryPrimitives.ReadInt32BigEndian(length);
            Assert.InRange(startupLength, 8, 65536);
            await stream.ReadExactlyAsync(new byte[startupLength - 4], deadline.Token);

            // PostgreSQL AuthenticationCleartextPassword (authentication code 3).
            await stream.WriteAsync(new byte[] { (byte)'R', 0, 0, 0, 8, 0, 0, 0, 3 }, deadline.Token);
            var header = new byte[5];
            await stream.ReadExactlyAsync(header, deadline.Token);
            Assert.Equal((byte)'p', header[0]);
            var passwordLength = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(1));
            Assert.InRange(passwordLength, 5, 1024);
            var payload = new byte[passwordLength - 4];
            await stream.ReadExactlyAsync(payload, deadline.Token);
            var received = Encoding.UTF8.GetString(payload).TrimEnd('\0');

            // Send a controlled FATAL response: the only successful behavior
            // expected by this test is forwarding the original dummy credential.
            var error = Encoding.UTF8.GetBytes("SFATAL\0C28P01\0MControlled test rejection\0\0");
            var response = new byte[5 + error.Length];
            response[0] = (byte)'E';
            BinaryPrimitives.WriteInt32BigEndian(response.AsSpan(1, 4), error.Length + 4);
            error.CopyTo(response, 5);
            await stream.WriteAsync(response, deadline.Token);
            return received;
        }, deadline.Token);

        var settings = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = endpoint.Port,
            Database = "test",
            Username = "test",
            Password = dummyPassword,
            SslMode = SslMode.Disable,
            Timeout = 5
        };
        await using var gates = new ParkingGateConnectionPool(settings.ConnectionString, 2);
        await Assert.ThrowsAnyAsync<NpgsqlException>(() => gates.TryOpenAsync(deadline.Token));
        Assert.Equal(dummyPassword, await peer);
    }
}
