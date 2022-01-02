using System.Net.Http;
using System.Threading;
using Newtonsoft.Json;

public class WeatherManager {
	public string CurrentTemperature = "loading...";

	public WeatherManager() {
		var thread = new Thread(UpdateWeatherThread);
		thread.Start();
	}

	async void UpdateWeatherThread() {
		while (true) {
			var client = new HttpClient();
			var url = "https://api.met.no/weatherapi/locationforecast/2.0/compact?lat=57.775891&lon=14.217855";
			var request = new HttpRequestMessage(HttpMethod.Get, url);
			request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 6.3; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.129 Safari/537.36");

			var response = await client.SendAsync(request);
			response.EnsureSuccessStatusCode(); // Throw an exception if error

			var body = await response.Content.ReadAsStringAsync();
			dynamic weather = JsonConvert.DeserializeObject(body);

			var temp = weather.properties.timeseries[0].data.instant.details.air_temperature;
			CurrentTemperature = $"{temp}°C";

			Thread.Sleep(10000);
		}
	}
}