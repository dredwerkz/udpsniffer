using System.Net.WebSockets;
using horizon.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
using Npgsql;
using NuGet.Protocol;

namespace horizon;

public class Startup
{
    private IConfiguration Configuration { get; }
    private static readonly List<WebSocket> ConnectedClients = new();

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<HorizonDbContext>(options =>
            options.UseNpgsql(Configuration.GetConnectionString(
                "DefaultConnection")));

        services.AddControllersWithViews(); // MVCs
    }

    /*private static async Task Echo(WebSocket webSocket)
    {
        // This literally just echoes ws messages back to the client, will throw an error on the front end as the result.MessageType is wrong!!
        var buffer = new byte[1024 * 4];
        var result =
            await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        while (result.CloseStatus.HasValue == false)
        {
            await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType,
                result.EndOfMessage, CancellationToken.None);
            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription,
            CancellationToken.None);

        ConnectedClients.Remove(webSocket);
    }*/

    private static async Task Echo(WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4];
        WebSocketReceiveResult result =
            await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        while (result.CloseStatus.HasValue == false)
        {
            // Convert the byte array to a string message
            string messageString = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
            var messageObject = Newtonsoft.Json.Linq.JObject.Parse(messageString);
            // Console.WriteLine($"messageString is: {messageString}");

            // Check if the message type is NEW_USER
            if (messageObject.ContainsKey("type") && messageObject["type"].ToString() == "NEW_USER")
            {
                // Create a predefined JSON result. Replace this with your actual JSON content.
                var jsonServerData = await GetAllServerData();
                string jsonResult = "{\"type\": \"SERVERS\", \"payload\":" + jsonServerData + "}";

                // Convert the JSON string to a byte array
                byte[] jsonResponseBytes = System.Text.Encoding.UTF8.GetBytes(jsonResult);

                // Send the JSON response
                await webSocket.SendAsync(new ArraySegment<byte>(jsonResponseBytes), WebSocketMessageType.Text, true,
                    CancellationToken.None);
            }
            else
            {
                // If it's not a NEW_USER message, echo back the received message
                await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType,
                    result.EndOfMessage, CancellationToken.None);
            }

            // Receive the next piece of data
            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        // Close the WebSocket connection gracefully
        await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);

        // Assuming ConnectedClients is a collection of WebSocket instances
        ConnectedClients.Remove(webSocket);
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseWebSockets();

        app.Use(async (context, next) =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                ConnectedClients.Add(webSocket);
                // Handling ws reqs goes in this block
                // I think for file management, I should create a separate class for handling ws and pass the message in from here 
                await Echo(webSocket);
            }
            else
            {
                await next(); // A request lands, we check if it's a WebSocket request - if so we HANDLE IT, if not we pass it down to the next middleware
            }
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(
                Path.Combine(Directory.GetCurrentDirectory(), "build")),
            RequestPath = ""
        });

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers(); // MVC
            endpoints.MapFallbackToFile("index.html");
        });
    }

    public static async Task
        BroadcastNewDataViaWebSocketAsync(object update, bool messageType) // Send server data via ws
    {
        // TODO: This is really sloppy, no real type enforcement on update - should probably be an interface :)
        var structuredMessage = new
        {
            type = messageType ? "UPDATE" : "SERVERS",
            payload = update
        };

        var jsonString = structuredMessage.ToJson();
        //Console.WriteLine(jsonString);

        var buffer = System.Text.Encoding.UTF8.GetBytes(jsonString);

        foreach (var webSocket in ConnectedClients.ToList().Where(webSocket => webSocket.State == WebSocketState.Open))
        {
            await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true,
                CancellationToken.None);
        }
    }

    private static async Task<string> GetAllServerData()
    {
        Console.WriteLine("GetAllServerData() called");
        var resultList = new List<Dictionary<string, object>>(); // List to hold your rows.

        using var connection =
            new NpgsqlConnection("Host=localhost;Database=postgres;Username=postgres;Password=asd123;");
        await connection.OpenAsync();

        using var cmd = new NpgsqlCommand("SELECT * FROM \"Servers\"", connection);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.GetValue(i);
            }

            resultList.Add(row);
        }

        // Convert the list to JSON
        string jsonResult = JsonConvert.SerializeObject(resultList, Formatting.Indented);
        //Console.WriteLine("Got stuff - sending:");
        //Console.WriteLine(jsonResult);
        return jsonResult;
    }
}