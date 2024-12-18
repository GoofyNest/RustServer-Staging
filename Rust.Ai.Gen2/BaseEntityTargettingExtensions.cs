namespace Rust.Ai.Gen2;

public static class BaseEntityTargettingExtensions
{
	public static bool InSameNpcTeam(this BaseEntity entity, BaseEntity other)
	{
		if (entity == null || other == null)
		{
			return false;
		}
		return entity.GetType() == other.GetType();
	}

	public static bool IsNonNpcPlayer(this BaseEntity entity)
	{
		BasePlayer basePlayer = entity.ToPlayer();
		if (basePlayer != null)
		{
			return !basePlayer.IsNpc;
		}
		return false;
	}

	public static bool IsNpcPlayer(this BaseEntity entity)
	{
		BasePlayer basePlayer = entity.ToPlayer();
		if (basePlayer != null)
		{
			return basePlayer.IsNpc;
		}
		return false;
	}

	public static bool ToNonNpcPlayer(this BaseEntity entity, out BasePlayer player)
	{
		BasePlayer basePlayer = entity.ToPlayer();
		if (basePlayer == null || basePlayer.IsNpc)
		{
			player = null;
			return false;
		}
		player = basePlayer;
		return true;
	}
}
