using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Microsoft.Data.Sqlite;
using System.Data;
using System.Net.Http;
using System.Text.Json;
using System;

while (true)
{
    string osuClientId = "47167";
    string osuClientSecret = "KumBja4WA8HqUL4r8AZdP0qlYYNi0rx7AhHia8G3";

    string credentialsPath = "credentials.json";

    GoogleCredential credential;
    using (var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read))
    {
        credential = GoogleCredential.FromFile(credentialsPath)
            .CreateScoped(SheetsService.Scope.Spreadsheets);
    }

    var initializer = new BaseClientService.Initializer();
    initializer.HttpClientInitializer = credential;
    initializer.ApplicationName = "Umd osu! Leaderboards";

    var sheetsService = new SheetsService(initializer);

    string spreadsheetId = "1upRzWWNP1aPNXc5ls_-hAEVKsmKy6K12E82bZ9XypTU";
    string range = "Form Responses 1!B2:B";

    var request = sheetsService.Spreadsheets.Values.Get(spreadsheetId, range);
    var response = request.Execute();
    var values = response.Values;

    string connectionString = "Data Source=leaderboard.db";
    var connection = new SqliteConnection(connectionString);
    connection.Open();

    string ExtractProfileId(string input)
    {
        // if its just a number
        if (int.TryParse(input, out _))
        {
            return input;
        }

        // if its a link, extract the id from the end
        if (input.Contains("osu.ppy.sh/users/"))
        {
            string[] parts = input.Split('/');
            return parts[parts.Length - 1];
        }

        // Else return the original
        return input;
    }

    if (values != null && values.Count > 0)
    {
        foreach (var row in values)
        {
            string input = row[0].ToString();
            string profileId = ExtractProfileId(input);

            Console.WriteLine($"Processing profile ID: {profileId}");

            // insert into db here
            string insertQuery = "INSERT OR IGNORE INTO users (profileId) VALUES (@profileId)";
            var command = new SqliteCommand(insertQuery, connection);
            command.Parameters.AddWithValue("@profileId", profileId);
            command.ExecuteNonQuery();
        }
    }
    else
    {
        Console.WriteLine("No data found.");
    }

    // osu!API

    var httpClient = new HttpClient();
    var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://osu.ppy.sh/oauth/token");
    var requestBody = new FormUrlEncodedContent(new Dictionary<string, string>
{
    { "client_id", osuClientId },
    { "client_secret", osuClientSecret },
    { "grant_type", "client_credentials" },
    { "scope", "public" }
});
    tokenRequest.Content = requestBody;

    var tokenResponse = httpClient.Send(tokenRequest);
    string responseBody = tokenResponse.Content.ReadAsStringAsync().Result;

    var tokenData = JsonSerializer.Deserialize<Dictionary<string, object>>(responseBody);
    string accessToken = tokenData["access_token"].ToString();

    string selectQuery = "SELECT profileId FROM users";
    var selectCommand = new SqliteCommand(selectQuery, connection);
    var reader = selectCommand.ExecuteReader();

    while (reader.Read())
    {
        string profileId = reader.GetString(0);
        Console.WriteLine($"Fetching data for profile ID: {profileId}");

        string apiUrl = $"https://osu.ppy.sh/api/v2/users/{profileId}/osu";

        var apiRequest = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        apiRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

        var apiResponse = httpClient.Send(apiRequest);
        string userDataJson = apiResponse.Content.ReadAsStringAsync().Result;

        var userData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(userDataJson);

        string username = userData["username"].GetString();
        var statistics = userData["statistics"];
        int globalRank = statistics.GetProperty("global_rank").GetInt32();
        int playTime = statistics.GetProperty("play_time").GetInt32() / 3600;
        int totalPP = (int)Math.Floor(statistics.GetProperty("pp").GetDouble());
        double hitAccuracy = Math.Round(statistics.GetProperty("hit_accuracy").GetDouble(), 2);
        double topPP = 0;
        double top5AvgPP = 0;

        // Get users top 5 plays

        string scoresURl = $"https://osu.ppy.sh/api/v2/users/{profileId}/scores/best?mode=osu&limit=5";
        var scoresRequest = new HttpRequestMessage(HttpMethod.Get, scoresURl);
        scoresRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

        var scoresResponse = httpClient.Send(scoresRequest);
        string scoresDataJson = scoresResponse.Content.ReadAsStringAsync().Result;
        var scoresData = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(scoresDataJson);

        int scoreCount = 0;
        double totalScorePP = 0;

        foreach (var score in scoresData)
        {
            double scorePP = score["pp"].GetDouble();
            totalScorePP += scorePP;
            scoreCount++;

            if (scorePP >= topPP)
            {
                topPP = scorePP;
            }
        }

        topPP = Math.Round(topPP, 1);
        top5AvgPP = scoreCount > 0 ? Math.Round(totalScorePP / scoreCount, 1) : 0;

        string updateQuery = "UPDATE users SET username = @username, globalRank = @globalRank, hitAccuracy = @hitAccuracy, playTime = @playTime, totalPP = @totalPP, topPP = @topPP, top5AvgPP = @top5AvgPP, lastSynced = @lastSynced WHERE profileId = @profileId";

        var updateCommand = new SqliteCommand(updateQuery, connection);
        updateCommand.Parameters.AddWithValue("@username", username);
        updateCommand.Parameters.AddWithValue("@globalRank", globalRank);
        updateCommand.Parameters.AddWithValue("@hitAccuracy", hitAccuracy);
        updateCommand.Parameters.AddWithValue("@playTime", playTime);
        updateCommand.Parameters.AddWithValue("@totalPP", totalPP);
        updateCommand.Parameters.AddWithValue("@topPP", topPP);
        updateCommand.Parameters.AddWithValue("@top5AvgPP", top5AvgPP);
        updateCommand.Parameters.AddWithValue("@profileId", profileId);
        updateCommand.Parameters.AddWithValue("@lastSynced", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        updateCommand.ExecuteNonQuery();

        Console.WriteLine($"Updated database for {username}");
    }

    reader.Close();

    string exportQuery = "SELECT profileId, username, globalRank, hitAccuracy, playTime, totalPP, topPP, top5AvgPP FROM users";
    var exportCommand = new SqliteCommand(exportQuery, connection);
    var exportReader = exportCommand.ExecuteReader();

    var players = new List<object>();

    while (exportReader.Read())
    {
        var player = new
        {
            profileId = exportReader.GetInt32(0),
            username = exportReader.GetString(1),
            globalRank = exportReader.GetInt32(2),
            hitAccuracy = exportReader.GetDouble(3),
            playTime = exportReader.GetInt32(4),
            totalPP = exportReader.GetDouble(5),
            topPP = exportReader.GetDouble(6),
            top5AvgPP = exportReader.GetDouble(7)
        };

        players.Add(player);
    }

    string json = JsonSerializer.Serialize(players, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText("C:\\Users\\chees\\OneDrive\\Documents\\VS\\OsuLocalLeaderboards\\WebPage\\leaderboard.json", json);
    Console.WriteLine(json);

    Console.WriteLine("Data exported successfully.");

    connection.Close();

    Console.WriteLine("Pushing to GitHub...");
    System.Diagnostics.Process.Start("update-github.bat");
    Console.WriteLine("Done!");

    Thread.Sleep(TimeSpan.FromMinutes(15));
}