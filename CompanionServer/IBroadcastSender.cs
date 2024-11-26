using System.Collections.Generic;

namespace CompanionServer;

public interface IBroadcastSender<TMessage>
{
	void BroadcastTo(List<Connection> targets, TMessage message);
}
