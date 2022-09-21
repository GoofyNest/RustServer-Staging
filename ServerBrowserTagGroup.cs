using System;
using System.Linq;
using Facepunch;
using UnityEngine;

public class ServerBrowserTagGroup : MonoBehaviour
{
	[Tooltip("If set then queries will filter out servers matching unselected tags in the group")]
	public bool isExclusive;

	[NonSerialized]
	public ServerBrowserTag[] tags;

	[NonSerialized]
	public string[] tagValues;

	private void Initialize()
	{
		if (tags == null || tagValues == null)
		{
			tags = GetComponentsInChildren<ServerBrowserTag>(includeInactive: true);
			tagValues = new string[tags.Length];
			for (int i = 0; i < tags.Length; i++)
			{
				tagValues[i] = tags[i].serverTag ?? "";
			}
		}
	}

	public void Awake()
	{
		Initialize();
	}

	public bool AnyActive()
	{
		ServerBrowserTag[] array = tags;
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].IsActive)
			{
				return true;
			}
		}
		return false;
	}

	public void Refresh(in ServerInfo server, ref int tagsEnabled, int maxTags)
	{
		Initialize();
		bool flag = false;
		ServerBrowserTag[] array = tags;
		foreach (ServerBrowserTag serverBrowserTag in array)
		{
			if ((!isExclusive || !flag) && tagsEnabled <= maxTags && server.Tags.Contains(serverBrowserTag.serverTag))
			{
				serverBrowserTag.SetActive(active: true);
				tagsEnabled++;
				flag = true;
			}
			else
			{
				serverBrowserTag.SetActive(active: false);
			}
		}
		base.gameObject.SetActive(flag);
	}
}
