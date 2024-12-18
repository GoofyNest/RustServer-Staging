using UnityEngine;

public class TriggeredEventPrefab : TriggeredEvent
{
	public GameObjectRef targetPrefab;

	public bool shouldBroadcastSpawn;

	public Translate.Phrase spawnPhrase;

	public BaseEntity spawnedEntity;

	public override void RunEvent()
	{
		Debug.Log("[event] " + targetPrefab.resourcePath);
		BaseEntity baseEntity = GameManager.server.CreateEntity(targetPrefab.resourcePath);
		if (!baseEntity)
		{
			return;
		}
		baseEntity.SendMessage("TriggeredEventSpawn", SendMessageOptions.DontRequireReceiver);
		baseEntity.Spawn();
		spawnedEntity = baseEntity;
		if (!shouldBroadcastSpawn)
		{
			return;
		}
		foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
		{
			if ((bool)activePlayer && activePlayer.IsConnected && !activePlayer.IsInTutorial)
			{
				activePlayer.ShowToast(GameTip.Styles.Server_Event, spawnPhrase, false);
			}
		}
	}

	public override void Kill()
	{
		if (!(spawnedEntity == null))
		{
			base.Kill();
			spawnedEntity.Kill();
			spawnedEntity = null;
			Debug.Log("Killed " + base.name);
		}
	}
}
