#define UNITY_ASSERTIONS
using System.Collections.Generic;
using Facepunch;
using UnityEngine.Assertions;

namespace Rust.Ai.Gen2;

public class LockState
{
	public class LockHandle
	{
	}

	private HashSet<LockHandle> locks = new HashSet<LockHandle>();

	public bool IsLocked => locks.Count > 0;

	public LockHandle AddLock()
	{
		LockHandle lockHandle = Pool.Get<LockHandle>();
		locks.Add(lockHandle);
		return lockHandle;
	}

	public bool RemoveLock(ref LockHandle handle)
	{
		if (handle == null)
		{
			return false;
		}
		bool num = locks.Remove(handle);
		Assert.IsTrue(num, "Trying to remove a lock that doesn't exist");
		if (num)
		{
			Pool.FreeUnsafe(ref handle);
		}
		return num;
	}
}
