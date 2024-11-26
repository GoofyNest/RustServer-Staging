using System;
using System.Net;
using Newtonsoft.Json;

[JsonModel]
public class IPAddressJsonConverter : JsonConverter
{
	public override bool CanConvert(Type objectType)
	{
		return objectType == typeof(IPAddress);
	}

	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
		string value2 = ((value is IPAddress iPAddress) ? iPAddress.ToString() : null);
		writer.WriteValue(value2);
	}

	public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
	{
		if (reader.TokenType != JsonToken.String)
		{
			return null;
		}
		if (!IPAddress.TryParse((string)reader.Value, out var address))
		{
			return null;
		}
		return address;
	}
}
