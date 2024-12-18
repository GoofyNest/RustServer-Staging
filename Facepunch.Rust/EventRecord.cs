using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Text;
using Newtonsoft.Json;
using UnityEngine;

namespace Facepunch.Rust;

public class EventRecord : Pool.IPooled
{
	public static readonly long TicksToNS = 1000000000 / Stopwatch.Frequency;

	public DateTime Timestamp;

	[NonSerialized]
	public bool IsServer;

	public List<EventRecordField> Data = new List<EventRecordField>();

	public int TimesCreated;

	public int TimesSubmitted;

	public string EventType { get; private set; }

	public void EnterPool()
	{
		Timestamp = default(DateTime);
		EventType = null;
		IsServer = false;
		Data.Clear();
	}

	public void LeavePool()
	{
	}

	public static EventRecord CSV()
	{
		EventRecord eventRecord = Pool.Get<EventRecord>();
		eventRecord.IsServer = true;
		eventRecord.TimesCreated++;
		return eventRecord;
	}

	public static EventRecord New(string type, bool isServer = true)
	{
		EventRecord eventRecord = Pool.Get<EventRecord>();
		eventRecord.EventType = type;
		eventRecord.AddField("type", type);
		eventRecord.AddField("guid", Guid.NewGuid());
		BuildInfo current = BuildInfo.Current;
		bool num = (current.Scm.Branch != null && current.Scm.Branch == "experimental/release") || current.Scm.Branch == "release";
		bool isEditor = UnityEngine.Application.isEditor;
		string value = ((num && !isEditor) ? "release" : (isEditor ? "editor" : "staging"));
		eventRecord.AddField("environment", value);
		eventRecord.IsServer = isServer;
		if (isServer && SaveRestore.WipeId != null)
		{
			eventRecord.AddField("wipe_id", SaveRestore.WipeId);
		}
		eventRecord.AddField("frame_count", Time.frameCount);
		eventRecord.Timestamp = DateTime.UtcNow;
		eventRecord.TimesCreated++;
		return eventRecord;
	}

	public EventRecord AddObject(string key, object data)
	{
		if (data == null)
		{
			return this;
		}
		Data.Add(new EventRecordField(key)
		{
			String = JsonConvert.SerializeObject(data),
			IsObject = true
		});
		return this;
	}

	public EventRecord SetTimestamp(DateTime timestamp)
	{
		Timestamp = timestamp;
		return this;
	}

	public EventRecord AddField(string key, DateTime time)
	{
		Data.Add(new EventRecordField(key)
		{
			DateTime = time
		});
		return this;
	}

	public EventRecord AddField(string key, bool value)
	{
		Data.Add(new EventRecordField(key)
		{
			String = (value ? "true" : "false")
		});
		return this;
	}

	public EventRecord AddField(string key, string value)
	{
		Data.Add(new EventRecordField(key)
		{
			String = value
		});
		return this;
	}

	public EventRecord AddField(string key, byte value)
	{
		return AddField(key, (int)value);
	}

	public EventRecord AddField(string key, sbyte value)
	{
		return AddField(key, (int)value);
	}

	public EventRecord AddField(string key, short value)
	{
		return AddField(key, (int)value);
	}

	public EventRecord AddField(string key, ushort value)
	{
		return AddField(key, (int)value);
	}

	public EventRecord AddField(string key, int value)
	{
		return AddField(key, (long)value);
	}

	public EventRecord AddField(string key, uint value)
	{
		return AddField(key, (long)value);
	}

	public EventRecord AddField(string key, ulong value)
	{
		return AddField(key, (long)value);
	}

	[Obsolete("Char not supported, either cast to int or string", true)]
	public EventRecord AddField(string key, char value)
	{
		throw new NotImplementedException();
	}

	public EventRecord AddField(string key, float value)
	{
		return AddField(key, (double)value);
	}

	public EventRecord AddField(string key, long value)
	{
		Data.Add(new EventRecordField(key)
		{
			Number = value
		});
		return this;
	}

	public EventRecord AddField(string key, double value)
	{
		Data.Add(new EventRecordField(key)
		{
			Float = value
		});
		return this;
	}

	public EventRecord AddField(string key, TimeSpan value)
	{
		Data.Add(new EventRecordField(key)
		{
			Number = value.Ticks * TicksToNS
		});
		return this;
	}

	public EventRecord AddLegacyTimespan(string key, TimeSpan value)
	{
		Data.Add(new EventRecordField(key)
		{
			Float = value.TotalSeconds
		});
		return this;
	}

	public EventRecord AddField(string key, Guid value)
	{
		Data.Add(new EventRecordField(key)
		{
			Guid = value
		});
		return this;
	}

	public EventRecord AddField(string key, Vector3 value)
	{
		Data.Add(new EventRecordField(key)
		{
			Vector = value
		});
		return this;
	}

	public EventRecord AddField(string key, BaseNetworkable entity)
	{
		if (entity == null || entity.net == null)
		{
			return this;
		}
		bool flag = EventType == "player_tick";
		if (entity is BasePlayer { IsNpc: false, IsBot: false } basePlayer)
		{
			string userWipeId = SingletonComponent<ServerMgr>.Instance.persistance.GetUserWipeId(basePlayer.userID);
			AddField(key, "_userid", userWipeId);
			AddField(key, "_modelstate", (basePlayer.modelStateTick ?? basePlayer.modelState).flags);
			if (flag)
			{
				AddField(key, "_pitch", basePlayer.tickViewAngles.x);
				AddField(key, "_yaw", basePlayer.tickViewAngles.y);
				AddField(key, "_pos_x", basePlayer.transform.position.x);
				AddField(key, "_pos_y", basePlayer.transform.position.y);
				AddField(key, "_pos_z", basePlayer.transform.position.z);
				AddField(key, "_mouse_delta_x", basePlayer.tickMouseDelta.x);
				AddField(key, "_mouse_delta_y", basePlayer.tickMouseDelta.y);
				AddField(key, "_mouse_delta_z", basePlayer.tickMouseDelta.z);
			}
			else
			{
				AddField(key, "_tickViewAngles", basePlayer.tickViewAngles);
				AddField(key, "_mouse_delta", basePlayer.tickMouseDelta);
			}
			AddField(key, "_heldentity", (basePlayer.GetHeldEntity() != null) ? basePlayer.GetHeldEntity().ShortPrefabName : "");
			AddField(key, "_mounted", basePlayer.GetMounted());
			AddField(key, "_parented", basePlayer.HasParent());
			if (!flag && (basePlayer.IsAdmin || basePlayer.IsDeveloper))
			{
				AddField(key, "_admin", value: true);
			}
		}
		if (entity is BaseEntity { skinID: not 0uL } baseEntity)
		{
			AddField(key, "_skin", baseEntity.skinID);
		}
		if (entity is BaseProjectile baseProjectile)
		{
			Item item = baseProjectile.GetItem();
			if (item != null && (item.contents?.itemList?.Count).GetValueOrDefault() > 0)
			{
				List<string> obj = Pool.Get<List<string>>();
				foreach (Item item2 in item.contents.itemList)
				{
					obj.Add(item2.info.shortname);
				}
				AddObject(key + "_inventory", obj);
				Pool.FreeUnmanaged(ref obj);
			}
		}
		if (entity is DroppedItem droppedItem && droppedItem.DroppedTime != default(DateTime) && droppedItem.DroppedTime >= DateTime.UnixEpoch)
		{
			string userWipeId2 = SingletonComponent<ServerMgr>.Instance.persistance.GetUserWipeId(droppedItem.DroppedBy);
			AddField("dropped_at", ((DateTimeOffset)droppedItem.DroppedTime).ToUnixTimeMilliseconds());
			AddField("dropped_by", userWipeId2);
		}
		if (entity is Door door)
		{
			AddField(key, "_building_id", door.buildingID);
		}
		if (entity is CodeLock codeLock && codeLock.GetParentEntity() != null && codeLock.GetParentEntity() is DecayEntity entity2)
		{
			AddField("parent", entity2);
		}
		if (entity is BuildingBlock buildingBlock)
		{
			AddField(key, "_grade", (int)buildingBlock.grade);
			AddField(key, "_building_id", (int)buildingBlock.buildingID);
		}
		if (!flag)
		{
			AddField(key, "_prefab", entity.ShortPrefabName);
			AddField(key, "_pos", entity.transform.position);
			AddField(key, "_rot", entity.transform.rotation.eulerAngles);
			AddField(key, "_id", entity.net.ID.Value);
		}
		return this;
	}

	public EventRecord AddField(string key, Item item)
	{
		if (item == null)
		{
			return this;
		}
		AddField(key, "_name", item.info.shortname);
		AddField(key, "_amount", item.amount);
		AddField(key, "_skin", item.skin);
		AddField(key, "_condition", item.conditionNormalized);
		return this;
	}

	public void MarkSubmitted()
	{
		TimesSubmitted++;
		if (TimesCreated != TimesSubmitted)
		{
			UnityEngine.Debug.LogError($"EventRecord pooling error: event has been submitted ({TimesSubmitted}) a different amount of times than it was created ({TimesCreated})");
		}
	}

	public void Submit()
	{
		if (IsServer)
		{
			Analytics.AzureWebInterface.server.EnqueueEvent(this);
		}
	}

	public void SerializeAsCSV(ref Utf8ValueStringBuilder writer)
	{
		if (Data.Count == 0)
		{
			return;
		}
		bool flag = false;
		foreach (EventRecordField datum in Data)
		{
			if (flag)
			{
				writer.Append(',');
			}
			else
			{
				flag = true;
			}
			writer.Append('"');
			datum.Serialize(ref writer, AnalyticsDocumentMode.CSV);
			writer.Append('"');
		}
	}

	public void SerializeAsJson(ref Utf8ValueStringBuilder writer, bool useDataObject = true)
	{
		writer.Append("{\"Timestamp\":\"");
		writer.Append(Timestamp, StandardFormats.DateTime_ISO);
		bool flag = false;
		if (useDataObject)
		{
			writer.Append("\",\"Data\":{");
		}
		else
		{
			writer.Append("\"");
			flag = true;
		}
		foreach (EventRecordField datum in Data)
		{
			if (flag)
			{
				writer.Append(',');
			}
			else
			{
				flag = true;
			}
			writer.Append("\"");
			writer.Append(datum.Key1);
			if (datum.Key2 != null)
			{
				writer.Append(datum.Key2);
			}
			writer.Append("\":");
			if (!datum.IsObject)
			{
				writer.Append('"');
			}
			datum.Serialize(ref writer, AnalyticsDocumentMode.JSON);
			if (!datum.IsObject)
			{
				writer.Append("\"");
			}
		}
		if (useDataObject)
		{
			writer.Append('}');
		}
		writer.Append('}');
	}

	public EventRecord AddField(byte value)
	{
		return AddField((long)value);
	}

	public EventRecord AddField(short value)
	{
		return AddField((long)value);
	}

	public EventRecord AddField(ushort value)
	{
		return AddField((long)value);
	}

	public EventRecord AddField(int value)
	{
		return AddField((long)value);
	}

	public EventRecord AddField(uint value)
	{
		return AddField((long)value);
	}

	public EventRecord AddField(ulong value)
	{
		return AddField((long)value);
	}

	public EventRecord AddField(float value)
	{
		return AddField((double)value);
	}

	[Obsolete("Char not supported, either cast to int or string")]
	public EventRecord AddField(char value)
	{
		throw new NotImplementedException();
	}

	public EventRecord AddField(long value)
	{
		Data.Add(new EventRecordField
		{
			Number = value
		});
		return this;
	}

	public EventRecord AddField(double value)
	{
		Data.Add(new EventRecordField
		{
			Float = value
		});
		return this;
	}

	public EventRecord AddField(string value)
	{
		Data.Add(new EventRecordField
		{
			String = value
		});
		return this;
	}

	public EventRecord AddField(bool value)
	{
		Data.Add(new EventRecordField
		{
			String = "true"
		});
		return this;
	}

	public EventRecord AddField(DateTime value)
	{
		Data.Add(new EventRecordField
		{
			DateTime = value
		});
		return this;
	}

	public EventRecord AddField(TimeSpan value)
	{
		Data.Add(new EventRecordField
		{
			Number = value.Ticks * TicksToNS
		});
		return this;
	}

	public EventRecord AddField(Guid value)
	{
		Data.Add(new EventRecordField
		{
			Guid = value
		});
		return this;
	}

	public EventRecord AddField(Vector3 vector)
	{
		Data.Add(new EventRecordField
		{
			Vector = vector
		});
		return this;
	}

	public EventRecord AddField(string key1, string key2, byte value)
	{
		return AddField(key1, key2, (long)value);
	}

	public EventRecord AddField(string key1, string key2, short value)
	{
		return AddField(key1, key2, (long)value);
	}

	public EventRecord AddField(string key1, string key2, ushort value)
	{
		return AddField(key1, key2, (long)value);
	}

	public EventRecord AddField(string key1, string key2, int value)
	{
		return AddField(key1, key2, (long)value);
	}

	public EventRecord AddField(string key1, string key2, uint value)
	{
		return AddField(key1, key2, (long)value);
	}

	public EventRecord AddField(string key1, string key2, ulong value)
	{
		return AddField(key1, key2, (long)value);
	}

	public EventRecord AddField(string key1, string key2, float value)
	{
		return AddField(key1, key2, (double)value);
	}

	[Obsolete("Char not supported, either cast to int or string")]
	public EventRecord AddField(string key1, string key2, char value)
	{
		throw new NotImplementedException();
	}

	public EventRecord AddField(string key1, string key2, long value)
	{
		Data.Add(new EventRecordField(key1, key2)
		{
			Number = value
		});
		return this;
	}

	public EventRecord AddField(string key1, string key2, double value)
	{
		Data.Add(new EventRecordField(key1, key2)
		{
			Float = value
		});
		return this;
	}

	public EventRecord AddField(string key1, string key2, string value)
	{
		Data.Add(new EventRecordField(key1, key2)
		{
			String = value
		});
		return this;
	}

	public EventRecord AddField(string key1, string key2, bool value)
	{
		Data.Add(new EventRecordField(key1, key2)
		{
			String = (value ? "true" : "false")
		});
		return this;
	}

	public EventRecord AddField(string key1, string key2, DateTime value)
	{
		Data.Add(new EventRecordField(key1, key2)
		{
			DateTime = value
		});
		return this;
	}

	public EventRecord AddField(string key1, string key2, TimeSpan value)
	{
		Data.Add(new EventRecordField(key1, key2)
		{
			Number = value.Ticks * TicksToNS
		});
		return this;
	}

	public EventRecord AddField(string key1, string key2, Guid value)
	{
		Data.Add(new EventRecordField(key1, key2)
		{
			Guid = value
		});
		return this;
	}

	public EventRecord AddField(string key1, string key2, Vector3 vector)
	{
		Data.Add(new EventRecordField(key1, key2)
		{
			Vector = vector
		});
		return this;
	}
}
