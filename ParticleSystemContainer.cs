using System;
using System.Collections.Generic;
using UnityEngine;

public class ParticleSystemContainer : MonoBehaviour, IPrefabPreProcess
{
	[Serializable]
	public struct ParticleSystemGroup
	{
		public ParticleSystem system;

		public LODComponentParticleSystem[] lodComponents;
	}

	public bool precached;

	public bool includeLights;

	[SerializeField]
	[HideInInspector]
	private ParticleSystemGroup[] particleGroups;

	[SerializeField]
	[HideInInspector]
	private Light[] lights;

	[SerializeField]
	[HideInInspector]
	private LightEx[] lightExs;

	public void Play()
	{
	}

	public void Pause()
	{
	}

	public void Stop()
	{
	}

	public void Clear()
	{
	}

	private void SetLights(bool on)
	{
		Light[] componentsInChildren;
		LightEx[] componentsInChildren2;
		if (precached)
		{
			componentsInChildren = lights;
			componentsInChildren2 = lightExs;
		}
		else
		{
			componentsInChildren = GetComponentsInChildren<Light>();
			componentsInChildren2 = GetComponentsInChildren<LightEx>();
		}
		LightEx[] array = componentsInChildren2;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].enabled = on;
		}
		Light[] array2 = componentsInChildren;
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i].enabled = on;
		}
	}

	public void PreProcess(IPrefabProcessor preProcess, GameObject rootObj, string name, bool serverside, bool clientside, bool bundling)
	{
		if (precached && clientside)
		{
			List<ParticleSystemGroup> list = new List<ParticleSystemGroup>();
			ParticleSystem[] componentsInChildren = GetComponentsInChildren<ParticleSystem>();
			foreach (ParticleSystem particleSystem in componentsInChildren)
			{
				LODComponentParticleSystem[] components = particleSystem.GetComponents<LODComponentParticleSystem>();
				ParticleSystemGroup particleSystemGroup = default(ParticleSystemGroup);
				particleSystemGroup.system = particleSystem;
				particleSystemGroup.lodComponents = components;
				ParticleSystemGroup item = particleSystemGroup;
				list.Add(item);
			}
			particleGroups = list.ToArray();
			if (includeLights)
			{
				lights = GetComponentsInChildren<Light>();
				lightExs = GetComponentsInChildren<LightEx>();
			}
		}
	}
}
