using System.Collections.Generic;
using System.Linq;

namespace ConVar;

public class CopyPasteHistoryManager
{
	public class PlayerHistory
	{
		public ulong SteamId;

		public List<PasteResult> History = new List<PasteResult>();

		public int CurrentIndex = -1;

		public PlayerHistory(ulong steamId)
		{
			SteamId = steamId;
		}
	}

	private List<PlayerHistory> playerHistory = new List<PlayerHistory>();

	public PlayerHistory GetOrCreateHistory(ulong steamId)
	{
		PlayerHistory playerHistory = this.playerHistory.FirstOrDefault((PlayerHistory x) => x.SteamId == steamId);
		if (playerHistory == null)
		{
			playerHistory = new PlayerHistory(steamId);
			this.playerHistory.Add(playerHistory);
		}
		return playerHistory;
	}

	public void AddToHistory(ulong steamId, List<BaseEntity> entities)
	{
		PlayerHistory orCreateHistory = GetOrCreateHistory(steamId);
		int num = orCreateHistory.History.Count - orCreateHistory.CurrentIndex - 1;
		if (num > 0)
		{
			orCreateHistory.History.RemoveRange(orCreateHistory.CurrentIndex + 1, num);
		}
		orCreateHistory.History.Add(new PasteResult(entities));
		orCreateHistory.CurrentIndex = orCreateHistory.History.Count - 1;
	}

	public PasteResult Undo(ulong steamId)
	{
		PlayerHistory orCreateHistory = GetOrCreateHistory(steamId);
		if (orCreateHistory.CurrentIndex < 0)
		{
			return null;
		}
		orCreateHistory.CurrentIndex--;
		return orCreateHistory.History[orCreateHistory.CurrentIndex + 1];
	}
}
