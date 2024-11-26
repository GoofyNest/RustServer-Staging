using UnityEngine;

public class PlayerDetectionTrigger : TriggerBase
{
	public GameObject detector;

	public IDetector _detector;

	private IDetector myDetector
	{
		get
		{
			if (_detector == null)
			{
				_detector = detector.GetComponent<IDetector>();
			}
			return _detector;
		}
	}

	internal override GameObject InterestedInObject(GameObject obj)
	{
		obj = base.InterestedInObject(obj);
		if (obj == null)
		{
			return null;
		}
		BaseEntity baseEntity = obj.ToBaseEntity();
		if (baseEntity == null)
		{
			return null;
		}
		if (baseEntity.isClient)
		{
			return null;
		}
		return baseEntity.gameObject;
	}

	internal override void OnObjects()
	{
		base.OnObjects();
		myDetector.OnObjects();
	}

	internal override void OnObjectAdded(GameObject obj, Collider col)
	{
		base.OnObjectAdded(obj, col);
		myDetector.OnObjectAdded(obj, col);
	}

	internal override void OnEmpty()
	{
		base.OnEmpty();
		myDetector.OnEmpty();
	}
}
