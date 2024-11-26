using Rust;
using UnityEngine;

public class SpawnPointInstance : MonoBehaviour
{
	internal BaseEntity Entity;

	internal ISpawnPointUser parentSpawnPointUser;

	internal BaseSpawnPoint parentSpawnPoint;

	public void Notify()
	{
		if (!parentSpawnPointUser.IsUnityNull())
		{
			parentSpawnPointUser.ObjectSpawned(this);
		}
		if ((bool)parentSpawnPoint)
		{
			parentSpawnPoint.ObjectSpawned(this);
		}
	}

	public void Retire()
	{
		if (!parentSpawnPointUser.IsUnityNull())
		{
			parentSpawnPointUser.ObjectRetired(this);
		}
		if ((bool)parentSpawnPoint)
		{
			parentSpawnPoint.ObjectRetired(this);
		}
	}

	protected void OnDestroy()
	{
		if (!Rust.Application.isQuitting)
		{
			Retire();
		}
	}
}
