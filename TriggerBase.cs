using System;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Facepunch;
using Rust;
using UnityEngine;

public class TriggerBase : BaseMonoBehaviour
{
	public LayerMask interestLayers;

	[NonSerialized]
	public HashSet<GameObject> contents;

	[NonSerialized]
	public HashSet<BaseEntity> entityContents;

	public Action<BaseNetworkable> OnEntityEnterTrigger;

	public Action<BaseNetworkable> OnEntityLeaveTrigger;

	public bool HasAnyContents => !contents.IsNullOrEmpty();

	public bool HasAnyEntityContents => !entityContents.IsNullOrEmpty();

	internal virtual GameObject InterestedInObject(GameObject obj)
	{
		int num = 1 << obj.layer;
		if ((interestLayers.value & num) != num)
		{
			return null;
		}
		return obj;
	}

	protected virtual void OnDisable()
	{
		if (!Rust.Application.isQuitting && contents != null)
		{
			GameObject[] array = contents.ToArray();
			foreach (GameObject targetObj in array)
			{
				OnTriggerExitImpl(targetObj);
			}
			contents = null;
		}
	}

	internal virtual void OnEntityEnter(BaseEntity ent)
	{
		if (!(ent == null))
		{
			if (entityContents == null)
			{
				entityContents = new HashSet<BaseEntity>();
			}
			entityContents.Add(ent);
			OnEntityEnterTrigger?.Invoke(ent);
		}
	}

	internal virtual void OnEntityLeave(BaseEntity ent)
	{
		if (entityContents != null)
		{
			entityContents.Remove(ent);
			OnEntityLeaveTrigger?.Invoke(ent);
		}
	}

	internal virtual void OnObjectAdded(GameObject obj, Collider col)
	{
		if (!(obj == null))
		{
			BaseEntity baseEntity = obj.ToBaseEntity();
			if ((bool)baseEntity)
			{
				baseEntity.EnterTrigger(this);
				OnEntityEnter(baseEntity);
			}
		}
	}

	internal virtual void OnObjectRemoved(GameObject obj)
	{
		if (obj == null)
		{
			return;
		}
		BaseEntity baseEntity = obj.ToBaseEntity(allowDestroyed: true);
		if (!baseEntity)
		{
			return;
		}
		bool flag = false;
		foreach (GameObject content in contents)
		{
			if (content == null)
			{
				Debug.LogWarning("Trigger " + ToString() + " contains null object.");
			}
			else if (content.ToBaseEntity(allowDestroyed: true) == baseEntity)
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			baseEntity.LeaveTrigger(this);
			OnEntityLeave(baseEntity);
		}
	}

	internal void RemoveInvalidEntities()
	{
		if (entityContents.IsNullOrEmpty())
		{
			return;
		}
		Collider component = GetComponent<Collider>();
		if (component == null)
		{
			return;
		}
		Bounds bounds = component.bounds;
		bounds.Expand(1f);
		List<BaseEntity> obj = null;
		foreach (BaseEntity entityContent in entityContents)
		{
			if (entityContent == null)
			{
				if (Debugging.checktriggers)
				{
					Debug.LogWarning("Trigger " + ToString() + " contains destroyed entity.");
				}
				if (obj == null)
				{
					obj = Facepunch.Pool.Get<List<BaseEntity>>();
				}
				obj.Add(entityContent);
			}
			else if (!bounds.Contains(entityContent.ClosestPoint(base.transform.position)))
			{
				if (Debugging.checktriggers)
				{
					Debug.LogWarning("Trigger " + ToString() + " contains entity that is too far away: " + entityContent.ToString());
				}
				if (obj == null)
				{
					obj = Facepunch.Pool.Get<List<BaseEntity>>();
				}
				obj.Add(entityContent);
			}
		}
		if (obj == null)
		{
			return;
		}
		foreach (BaseEntity item in obj)
		{
			RemoveEntity(item);
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	internal bool CheckEntity(BaseEntity ent)
	{
		if (ent == null)
		{
			return true;
		}
		Collider component = GetComponent<Collider>();
		if (component == null)
		{
			return true;
		}
		Bounds bounds = component.bounds;
		bounds.Expand(1f);
		return bounds.Contains(ent.ClosestPoint(base.transform.position));
	}

	internal virtual void OnObjects()
	{
	}

	internal virtual void OnEmpty()
	{
		contents = null;
		entityContents = null;
	}

	public void RemoveObject(GameObject obj)
	{
		if (!(obj == null))
		{
			Collider component = obj.GetComponent<Collider>();
			if (!(component == null))
			{
				OnTriggerExit(component);
			}
		}
	}

	public void RemoveEntity(BaseEntity ent)
	{
		if (this == null || contents == null || ent == null)
		{
			return;
		}
		List<GameObject> obj = Facepunch.Pool.Get<List<GameObject>>();
		foreach (GameObject content in contents)
		{
			if (content != null && content.ToBaseEntity(allowDestroyed: true) == ent)
			{
				obj.Add(content);
			}
		}
		foreach (GameObject item in obj)
		{
			OnTriggerExitImpl(item);
		}
		Facepunch.Pool.FreeUnmanaged(ref obj);
	}

	public void OnTriggerEnter(Collider collider)
	{
		if (this == null || !base.enabled)
		{
			return;
		}
		using (TimeWarning.New("TriggerBase.OnTriggerEnter"))
		{
			GameObject gameObject = InterestedInObject(collider.gameObject);
			if (gameObject == null)
			{
				return;
			}
			if (contents == null)
			{
				contents = new HashSet<GameObject>();
			}
			if (contents.Contains(gameObject))
			{
				return;
			}
			int count = contents.Count;
			contents.Add(gameObject);
			OnObjectAdded(gameObject, collider);
			if (count == 0 && contents.Count == 1)
			{
				OnObjects();
			}
		}
		if (Debugging.checktriggers)
		{
			RemoveInvalidEntities();
		}
	}

	internal virtual bool SkipOnTriggerExit(Collider collider)
	{
		return false;
	}

	public void OnTriggerExit(Collider collider)
	{
		if (this == null || collider == null || SkipOnTriggerExit(collider))
		{
			return;
		}
		GameObject gameObject = InterestedInObject(collider.gameObject);
		if (!(gameObject == null))
		{
			OnTriggerExitImpl(gameObject);
			if (Debugging.checktriggers)
			{
				RemoveInvalidEntities();
			}
		}
	}

	private void OnTriggerExitImpl(GameObject targetObj)
	{
		if (contents != null && contents.Contains(targetObj))
		{
			contents.Remove(targetObj);
			OnObjectRemoved(targetObj);
			if (contents == null || contents.Count == 0)
			{
				OnEmpty();
			}
		}
	}
}
