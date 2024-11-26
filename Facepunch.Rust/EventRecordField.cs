using System;
using Cysharp.Text;
using UnityEngine;

namespace Facepunch.Rust;

public struct EventRecordField
{
	public string Key1;

	public string Key2;

	public string String;

	public long? Number;

	public double? Float;

	public Vector3? Vector;

	public Guid? Guid;

	public DateTime DateTime;

	public bool IsObject;

	public EventRecordField(string key1)
	{
		Key1 = key1;
		Key2 = null;
		String = null;
		Number = null;
		Float = null;
		Vector = null;
		Guid = null;
		IsObject = false;
		DateTime = default(DateTime);
	}

	public EventRecordField(string key1, string key2)
	{
		Key1 = key1;
		Key2 = key2;
		String = null;
		Number = null;
		Float = null;
		Vector = null;
		Guid = null;
		IsObject = false;
		DateTime = default(DateTime);
	}

	public void Serialize(ref Utf8ValueStringBuilder writer, AnalyticsDocumentMode format)
	{
		if (String != null)
		{
			if (IsObject)
			{
				writer.Append(String);
				return;
			}
			string @string = String;
			int length = String.Length;
			for (int i = 0; i < length; i++)
			{
				char c = @string[i];
				if (c == '\\' && format == AnalyticsDocumentMode.JSON)
				{
					writer.Append("\\\\");
					continue;
				}
				switch (c)
				{
				case '"':
					if (format == AnalyticsDocumentMode.JSON)
					{
						writer.Append("\\\"");
					}
					else
					{
						writer.Append("\"\"");
					}
					break;
				case '\n':
					writer.Append("\\n");
					break;
				case '\r':
					writer.Append("\\r");
					break;
				case '\t':
					writer.Append("\\t");
					break;
				default:
					writer.Append(c);
					break;
				}
			}
		}
		else if (Float.HasValue)
		{
			writer.Append(Float.Value);
		}
		else if (Number.HasValue)
		{
			writer.Append(Number.Value);
		}
		else if (Guid.HasValue)
		{
			writer.Append(Guid.Value.ToString("N"));
		}
		else if (Vector.HasValue)
		{
			writer.Append('(');
			Vector3 value = Vector.Value;
			writer.Append(value.x);
			writer.Append(',');
			writer.Append(value.y);
			writer.Append(',');
			writer.Append(value.z);
			writer.Append(')');
		}
		else if (DateTime != default(DateTime))
		{
			writer.Append(DateTime, StandardFormats.DateTime_ISO);
		}
	}
}
