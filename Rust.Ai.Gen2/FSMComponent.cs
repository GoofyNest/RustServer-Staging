using System.Collections.Generic;
using Facepunch;
using UnityEngine;

namespace Rust.Ai.Gen2;

[SoftRequireComponent(typeof(LimitedTurnNavAgent), typeof(RootMotionPlayer), typeof(SenseComponent))]
[SoftRequireComponent(typeof(BlackboardComponent), typeof(NPCEncounterTimer))]
public class FSMComponent : EntityComponent<BaseEntity>, IServerComponent
{
	public class TreeNode
	{
		public FSMStateBase state;

		public List<(FSMTransitionBase transition, TreeNode dstState)> tickTransitions;

		public List<(FSMTransitionBase transition, TreeNode dstState, EFSMStateStatus status)> endTransitions;

		public List<TreeNode> children;

		public TreeNode parent;

		public TreeNode(FSMStateBase state = null)
		{
			this.state = state;
		}

		public bool IsLeaf()
		{
			return children == null;
		}

		public TreeNode GetFirstChildLeaf()
		{
			if (IsLeaf())
			{
				return this;
			}
			foreach (TreeNode child in children)
			{
				if (child.IsLeaf())
				{
					return child;
				}
				TreeNode firstChildLeaf = child.GetFirstChildLeaf();
				if (firstChildLeaf != null)
				{
					return firstChildLeaf;
				}
			}
			return null;
		}

		public void GetAllNestedStates(HashSet<FSMStateBase> nestedStates)
		{
			if (IsLeaf())
			{
				return;
			}
			foreach (TreeNode child in children)
			{
				if (child.IsLeaf())
				{
					nestedStates.Add(child.state);
				}
				else
				{
					child.GetAllNestedStates(nestedStates);
				}
			}
		}

		public TreeNode AddEndTransition(TreeNode dstState, FSMTransitionBase transition = null)
		{
			if (endTransitions == null)
			{
				endTransitions = new List<(FSMTransitionBase, TreeNode, EFSMStateStatus)>();
			}
			endTransitions.Add((transition, dstState, EFSMStateStatus.Success | EFSMStateStatus.Failure));
			return this;
		}

		public TreeNode AddFailureTransition(TreeNode dstState)
		{
			if (endTransitions == null)
			{
				endTransitions = new List<(FSMTransitionBase, TreeNode, EFSMStateStatus)>();
			}
			endTransitions.Add((null, dstState, EFSMStateStatus.Failure));
			return this;
		}

		public TreeNode AddTickTransition(TreeNode dstState, FSMTransitionBase transition)
		{
			if (tickTransitions == null)
			{
				tickTransitions = new List<(FSMTransitionBase, TreeNode)>();
			}
			tickTransitions.Add((transition, dstState));
			return this;
		}

		public TreeNode AddTickBranchingTrans(TreeNode dstState1, FSMTransitionBase sharedTransition, TreeNode dstState2, FSMTransitionBase dstState2Trans)
		{
			if (tickTransitions == null)
			{
				tickTransitions = new List<(FSMTransitionBase, TreeNode)>();
			}
			tickTransitions.Add((new Trans_And { sharedTransition, dstState2Trans }, dstState2));
			tickTransitions.Add((sharedTransition, dstState1));
			return this;
		}

		public TreeNode AddChild(TreeNode child)
		{
			if (state != null)
			{
				Debug.LogError("Can't add children to node with a state");
			}
			if (children == null)
			{
				children = new List<TreeNode>();
			}
			children.Add(child);
			child.parent = this;
			return child;
		}

		public TreeNode AddChildren(TreeNode child1, TreeNode child2 = null, TreeNode child3 = null, TreeNode child4 = null, TreeNode child5 = null, TreeNode child6 = null, TreeNode child7 = null, TreeNode child8 = null, TreeNode child9 = null, TreeNode child10 = null)
		{
			AddChild(child1);
			if (child2 != null)
			{
				AddChild(child2);
			}
			if (child3 != null)
			{
				AddChild(child3);
			}
			if (child4 != null)
			{
				AddChild(child4);
			}
			if (child5 != null)
			{
				AddChild(child5);
			}
			if (child6 != null)
			{
				AddChild(child6);
			}
			if (child7 != null)
			{
				AddChild(child7);
			}
			if (child8 != null)
			{
				AddChild(child8);
			}
			if (child9 != null)
			{
				AddChild(child9);
			}
			if (child10 != null)
			{
				AddChild(child10);
			}
			return this;
		}
	}

	public class TickFSMWorkQueue : PersistentObjectWorkQueue<FSMComponent>
	{
		protected override void RunJob(FSMComponent component)
		{
			if (ShouldAdd(component) && component.enabled)
			{
				component.Senses.Tick();
				component.GetComponent<NPCEncounterTimer>().Tick();
				component.Tick();
			}
		}

		protected override bool ShouldAdd(FSMComponent component)
		{
			if (base.ShouldAdd(component))
			{
				return component.baseEntity.IsValid();
			}
			return false;
		}
	}

	private bool isRunning;

	private SenseComponent _senses;

	[ServerVar]
	public static float minRefreshIntervalSeconds = 0f;

	[ServerVar]
	public static float maxRefreshIntervalSeconds = 0.5f;

	private double? _lastTickTime;

	private double nextRefreshTime;

	private const int maxStateChangesPerTick = 3;

	private int numberOfStateChangesThisTick;

	public FSMStateBase pendingStateChange;

	public static TickFSMWorkQueue workQueue = new TickFSMWorkQueue();

	[ServerVar(Help = "How many milliseconds to spend on the AIs FSMs per frame")]
	public static float frameBudgetMs = 0.5f;

	public FSMStateBase CurrentState { get; private set; }

	private SenseComponent Senses => _senses ?? (_senses = base.baseEntity.GetComponent<SenseComponent>());

	private float RefreshInterval
	{
		get
		{
			if (!Senses.ShouldRefreshFast)
			{
				return maxRefreshIntervalSeconds;
			}
			return minRefreshIntervalSeconds;
		}
	}

	private double LastTickTime
	{
		get
		{
			double valueOrDefault = _lastTickTime.GetValueOrDefault();
			if (!_lastTickTime.HasValue)
			{
				valueOrDefault = Time.timeAsDouble;
				_lastTickTime = valueOrDefault;
				return valueOrDefault;
			}
			return valueOrDefault;
		}
		set
		{
			_lastTickTime = value;
		}
	}

	public void Run()
	{
		if (isRunning)
		{
			Debug.LogWarning("[FSM] Trying to start a FSM that's already running on " + base.baseEntity.gameObject.name);
			return;
		}
		isRunning = true;
		_lastTickTime = null;
		workQueue.Add(this);
	}

	public void Stop()
	{
		if (!isRunning)
		{
			Debug.LogWarning("[FSM] Trying to stop a FSM that is not running on " + base.baseEntity.gameObject.name);
			return;
		}
		isRunning = false;
		workQueue.Remove(this);
	}

	private void OnDestroy()
	{
		Stop();
	}

	public static void ShowDebugInfoAroundLocation(BasePlayer player, float radius = 100f)
	{
		if (!player.IsValid())
		{
			return;
		}
		using PooledList<BaseEntity> pooledList = Pool.Get<PooledList<BaseEntity>>();
		BaseEntity.Query.Server.GetBrainsInSphere(player.transform.position, radius, pooledList);
		foreach (BaseEntity item in pooledList)
		{
			FSMComponent component = item.GetComponent<FSMComponent>();
			if (!(component == null) && component.CurrentState != null && component.isRunning)
			{
				player.ClientRPC(RpcTarget.Player("CL_ShowStateDebugInfo", player), component.baseEntity.transform.position, component.CurrentState.Name);
			}
		}
	}

	public void Tick()
	{
		using (TimeWarning.New("FSMComponent.Tick"))
		{
			if (Time.timeAsDouble < nextRefreshTime)
			{
				return;
			}
			nextRefreshTime = Time.timeAsDouble + (double)RefreshInterval;
			float deltaTime = (float)(Time.timeAsDouble - LastTickTime);
			LastTickTime = Time.timeAsDouble;
			numberOfStateChangesThisTick = 0;
			if (pendingStateChange != null)
			{
				SetState(pendingStateChange);
			}
			else
			{
				if (CurrentState == null)
				{
					return;
				}
				using (TimeWarning.New("NormalTransitions"))
				{
					foreach (var (fSMTransitionBase, fSMStateBase) in CurrentState.transitions)
					{
						if (fSMTransitionBase.Evaluate())
						{
							fSMStateBase.Owner = base.baseEntity;
							fSMTransitionBase.OnTransitionTaken(CurrentState, fSMStateBase);
							SetState(fSMStateBase);
							return;
						}
					}
				}
				EFSMStateStatus currentStateStatus = EFSMStateStatus.None;
				using (TimeWarning.New("StateTick"))
				{
					using (TimeWarning.New(CurrentState.Name))
					{
						currentStateStatus = CurrentState.OnStateUpdate(deltaTime);
					}
				}
				EvaluateEndTransitions(currentStateStatus);
			}
		}
	}

	private void EvaluateEndTransitions(EFSMStateStatus currentStateStatus)
	{
		using (TimeWarning.New("EndTransitions"))
		{
			if (currentStateStatus == EFSMStateStatus.None)
			{
				return;
			}
			foreach (var (fSMTransitionBase, fSMStateBase, eFSMStateStatus) in CurrentState.endTransitions)
			{
				if ((eFSMStateStatus == (EFSMStateStatus.Success | EFSMStateStatus.Failure) || eFSMStateStatus == currentStateStatus) && (fSMTransitionBase == null || fSMTransitionBase.Evaluate()))
				{
					fSMStateBase.Owner = base.baseEntity;
					fSMTransitionBase?.OnTransitionTaken(CurrentState, fSMStateBase);
					SetState(fSMStateBase);
					break;
				}
			}
		}
	}

	public void SetState(FSMStateBase newState)
	{
		using (TimeWarning.New("SetState"))
		{
			pendingStateChange = null;
			numberOfStateChangesThisTick++;
			if (numberOfStateChangesThisTick > 3)
			{
				Debug.LogError("[FSM] Possible endless recursion detected from " + CurrentState?.Name + " to " + newState.Name + " on " + base.baseEntity.name);
				pendingStateChange = newState;
				return;
			}
			if (CurrentState != null)
			{
				using (TimeWarning.New("Transitions OnStateExit"))
				{
					foreach (var endTransition in CurrentState.endTransitions)
					{
						endTransition.transition?.OnStateExit();
					}
					foreach (var transition in CurrentState.transitions)
					{
						transition.transition.OnStateExit();
					}
				}
				using (TimeWarning.New("OnStateExit"))
				{
					using (TimeWarning.New(CurrentState.Name))
					{
						CurrentState.OnStateExit();
					}
				}
			}
			CurrentState = newState;
			using (TimeWarning.New("Transitions OnStateEnter"))
			{
				foreach (var endTransition2 in CurrentState.endTransitions)
				{
					endTransition2.transition?.OnStateEnter();
				}
				foreach (var transition2 in CurrentState.transitions)
				{
					transition2.transition.OnStateEnter();
				}
			}
			using (TimeWarning.New("OnStateEnter"))
			{
				using (TimeWarning.New(CurrentState.Name))
				{
					EFSMStateStatus currentStateStatus = CurrentState.OnStateEnter();
					EvaluateEndTransitions(currentStateStatus);
				}
			}
		}
	}

	public T AddTransition<T>(FSMStateBase from, FSMStateBase to, T transition) where T : FSMTransitionBase
	{
		transition.Init(base.baseEntity);
		from.transitions.Add((transition, to));
		return transition;
	}

	public FSMTransitionBase AddEndTransition(FSMStateBase from, FSMStateBase to, FSMTransitionBase transition = null, EFSMStateStatus status = EFSMStateStatus.Success | EFSMStateStatus.Failure)
	{
		transition?.Init(base.baseEntity);
		from.endTransitions.Add((transition, to, status));
		return transition;
	}

	public T AddTransitionMulti<T>(HashSet<FSMStateBase> from, FSMStateBase to, T transition) where T : FSMTransitionBase
	{
		transition.Init(base.baseEntity);
		foreach (FSMStateBase item in from)
		{
			item.transitions.Add((transition, to));
		}
		return transition;
	}

	public void BuildFromTree(TreeNode node)
	{
		if (!node.IsLeaf())
		{
			HashSet<FSMStateBase> hashSet = new HashSet<FSMStateBase>();
			node.GetAllNestedStates(hashSet);
			if (node.tickTransitions != null)
			{
				foreach (var (transition, treeNode) in node.tickTransitions)
				{
					AddTransitionMulti(hashSet, treeNode.GetFirstChildLeaf().state, transition);
				}
			}
			{
				foreach (TreeNode child in node.children)
				{
					BuildFromTree(child);
				}
				return;
			}
		}
		if (node.tickTransitions != null)
		{
			foreach (var (transition2, treeNode2) in node.tickTransitions)
			{
				AddTransition(node.state, treeNode2.GetFirstChildLeaf().state, transition2);
			}
		}
		if (node.endTransitions == null)
		{
			return;
		}
		foreach (var (transition3, treeNode3, status) in node.endTransitions)
		{
			AddEndTransition(node.state, treeNode3.GetFirstChildLeaf().state, transition3, status);
		}
	}
}
