using UnityEngine;

public class VehicleSpawnPoint : SpaceCheckingSpawnPoint
{
	public override void ObjectSpawned(SpawnPointInstance instance)
	{
		base.ObjectSpawned(instance);
		AddStartingFuel(instance.gameObject.ToBaseEntity() as BaseVehicle);
	}

	public static void AddStartingFuel(BaseVehicle vehicle)
	{
		if (!(vehicle == null))
		{
			vehicle.GetFuelSystem()?.AddStartingFuel(vehicle.StartingFuelUnits());
		}
	}
}
