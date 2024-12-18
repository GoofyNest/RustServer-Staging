namespace UnityEngine;

public class CachedMonumentComponent : MonoBehaviour
{
	public MonumentInfo Monument;

	public Vector3 LastPosition;

	public void UpdateMonument(MonumentInfo info, Collider collider)
	{
		Monument = info;
		LastPosition = collider.transform.position;
	}
}
