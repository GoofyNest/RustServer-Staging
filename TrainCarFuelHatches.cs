using UnityEngine;

public class TrainCarFuelHatches : MonoBehaviour
{
	private enum HatchState
	{
		Closed,
		Open,
		Opening,
		Closing
	}

	[SerializeField]
	private TrainCar owner;

	[SerializeField]
	private float animSpeed = 1f;

	[SerializeField]
	private Transform hatch1Col;

	[SerializeField]
	private Transform hatch1Vis;

	[SerializeField]
	private Transform hatch2Col;

	[SerializeField]
	private Transform hatch2Vis;

	[SerializeField]
	private Transform hatch3Col;

	[SerializeField]
	private Transform hatch3Vis;

	private const float closedXAngle = 0f;

	private const float openXAngle = -145f;

	[SerializeField]
	private SoundDefinition hatchOpenSoundDef;

	[SerializeField]
	private SoundDefinition hatchCloseSoundDef;

	private Vector3 _angles = Vector3.zero;

	private float _hatchLerp;

	private HatchState hatchState;

	public void ClientTick()
	{
		CoalingTower.IsUnderAnUnloader(owner, out var isLinedUp, out var _);
		switch (hatchState)
		{
		case HatchState.Closed:
			if (isLinedUp)
			{
				hatchState = HatchState.Opening;
				_hatchLerp = 0f;
			}
			break;
		case HatchState.Open:
			if (!isLinedUp)
			{
				hatchState = HatchState.Closing;
				_hatchLerp = 0f;
			}
			break;
		case HatchState.Opening:
			_hatchLerp += Time.deltaTime * animSpeed;
			if (_hatchLerp >= 1f)
			{
				hatchState = HatchState.Open;
			}
			else
			{
				SetAngleOnAll(_hatchLerp, closing: false);
			}
			break;
		case HatchState.Closing:
			_hatchLerp += Time.deltaTime * animSpeed;
			if (_hatchLerp >= 1f)
			{
				hatchState = HatchState.Closed;
			}
			else
			{
				SetAngleOnAll(_hatchLerp, closing: true);
			}
			break;
		}
	}

	public void StopClientTick()
	{
		ClientTick();
		if (hatchState == HatchState.Closing || hatchState == HatchState.Opening)
		{
			InvokeHandler.InvokeRepeating(this, BackupTick, 0f, 0f);
		}
	}

	private void BackupTick()
	{
		ClientTick();
		if (hatchState == HatchState.Closed || hatchState == HatchState.Open)
		{
			InvokeHandler.CancelInvoke(this, BackupTick);
		}
	}

	private void SetAngleOnAll(float lerpT, bool closing)
	{
		float angle;
		float angle2;
		float angle3;
		if (closing)
		{
			angle = LeanTween.easeOutBounce(-145f, 0f, Mathf.Clamp01(lerpT * 1.15f));
			angle2 = LeanTween.easeOutBounce(-145f, 0f, lerpT);
			angle3 = LeanTween.easeOutBounce(-145f, 0f, Mathf.Clamp01(lerpT * 1.25f));
		}
		else
		{
			angle = LeanTween.easeOutBounce(0f, -145f, Mathf.Clamp01(lerpT * 1.15f));
			angle2 = LeanTween.easeOutBounce(0f, -145f, lerpT);
			angle3 = LeanTween.easeOutBounce(0f, -145f, Mathf.Clamp01(lerpT * 1.25f));
		}
		SetAngle(hatch1Col, angle);
		SetAngle(hatch2Col, angle2);
		SetAngle(hatch3Col, angle3);
	}

	private void SetAngle(Transform transform, float angle)
	{
		_angles.x = angle;
		transform.localEulerAngles = _angles;
	}
}
