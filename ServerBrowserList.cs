using System;
using Facepunch;
using UnityEngine;

public class ServerBrowserList : ServerBrowserListBase, VirtualScroll.IDataSource, VirtualScroll.IVisualUpdate
{
	[Serializable]
	public struct Rules
	{
		public string tag;

		public ServerBrowserList serverList;

		public string CompressedTag { get; set; }
	}

	public enum QueryType
	{
		RegularInternet,
		Friends,
		History,
		LAN,
		Favourites,
		None
	}

	[Serializable]
	public struct ServerKeyvalues
	{
		public string key;

		public string value;
	}

	public bool startActive;

	public Transform listTransform;

	public int refreshOrder;

	public bool UseOfficialServers;

	public VirtualScroll VirtualScroll;

	public Rules[] rules;

	public bool hideOfficialServers;

	public bool excludeEmptyServersUsingQuery;

	public bool alwaysIncludeEmptyServers;

	public bool clampPlayerCountsToTrustedValues = true;

	private static string[] pingStrings = new string[3] { ".", "..", "..." };

	public QueryType queryType;

	public static string VersionTag = "v" + 2571;

	public ServerKeyvalues[] keyValues = new ServerKeyvalues[0];

	public int GetItemCount()
	{
		return 0;
	}

	public void OnVisualUpdate(int i, GameObject obj)
	{
	}

	public void SetItemData(int i, GameObject obj)
	{
	}
}
