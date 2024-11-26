using System.Collections.Generic;

namespace ConVar;

public class PasteResult
{
	public List<BaseEntity> Entities;

	public PasteResult(List<BaseEntity> entities)
	{
		Entities = entities;
	}
}
