using Facepunch;

namespace UnityEngine;

public static class ComponentEx
{
	public static T Instantiate<T>(this T component) where T : Component
	{
		return Facepunch.Instantiate.GameObject(component.gameObject).GetComponent<T>();
	}

	public static bool HasComponent<T>(this Component component) where T : Component
	{
		return component.GetComponent<T>() != null;
	}

	public static bool? IsEnabled(this Component component)
	{
		if (component is Behaviour behaviour)
		{
			return behaviour.enabled;
		}
		if (component is Collider collider)
		{
			return collider.enabled;
		}
		if (component is Renderer renderer)
		{
			return renderer.enabled;
		}
		if (component is ParticleSystem { emission: var emission })
		{
			return emission.enabled;
		}
		if (component is LODGroup lODGroup)
		{
			return lODGroup.enabled;
		}
		return null;
	}
}
