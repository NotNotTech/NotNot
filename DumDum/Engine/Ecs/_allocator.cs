﻿using DumDum.Bcl;
using DumDum.Bcl.Diagnostics;
using Microsoft.Toolkit.HighPerformance.Buffers;
using Microsoft.Toolkit.HighPerformance.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DumDum.Engine.Allocation;

/// <summary>
/// just a hint that the derived object is a component.  not actually needed.
/// </summary>
public interface IComponent
{

}

/// <summary>
/// only good for the current frame, unless chunk packing is disabled.  in that case it's good for lifetime of entity in the archetype.
/// </summary>
public record struct AllocToken : IComparable<AllocToken>
{
	public bool isAlive;
	public long externalId;


	/// <summary>
	/// the id of the allocator that created/tracks this allocSlot.  //TODO: make the archetype also use this ID as it's own (on create allocator, use it's ID)
	/// If needed, the allocator can be accessed via `Allocator._GLOBAL_LOOKUP(allocatorId)`
	/// </summary>
	public int allocatorId;
	public AllocSlot allocSlot;
	/// <summary>
	/// needs to match Allocator._packVersion, otherwise a pack took place and the token needs to be refreshed.
	/// </summary>
	public int packVersion;
	/// <summary>
	/// can be used to directly find a chunk from `Chunk[TComponent]._GLOBAL_LOOKUP(chunkId)`
	/// </summary>
	public long GetChunkLookupId()
	{
		//create a long from two ints
		//long correct = (long)left << 32 | (long)(uint)right;  //from: https://stackoverflow.com/a/33325313/1115220
#if CHECKED

		var chunkLookup = new ChunkLookupId { allocatorId = allocatorId, columnChunkIndex = allocSlot.columnChunkIndex };
		var chunkLookupId = chunkLookup._packedValue;
		var toReturn = (long)allocatorId << 32 | (uint)allocSlot.columnChunkIndex;
		//var toReturn = (long)allocSlot.columnChunkIndex<< 32 | (uint)allocatorId;
		__CHECKED.Throw(chunkLookupId == toReturn, "ints to long is wrong");
#endif




		//return (long)allocSlot.columnChunkIndex << 32 | (uint)allocatorId;
		return (long)allocatorId << 32 | (uint)allocSlot.columnChunkIndex;
	}

	public ref T GetComponentWriteRef<T>()
	{
		_CHECKED_VerifyInstance<T>();
		var chunk = GetContainingChunk<T>();
		return ref chunk.GetWriteRef(this);

	}
	public ref readonly T GetComponentReadRef<T>()
	{
		_CHECKED_VerifyInstance<T>();
		var chunk = GetContainingChunk<T>();
		return ref chunk.GetReadRef(this);
	}
	public Chunk<T> GetContainingChunk<T>()
	{
		_CHECKED_VerifyInstance<T>();
		var chunkLookupId = GetChunkLookupId();

		lock (Chunk<T>._GLOBAL_LOOKUP)
		{
			if (!Chunk<T>._GLOBAL_LOOKUP.TryGetValue(chunkLookupId, out var chunk))
			{
				__ERROR.Throw(GetAllocator().HasComponent<T>(), $"the archetype this element is attached to does not have a component of type {typeof(T).FullName}. Be aware that base classes do not match.");
				//need to refresh token
				__ERROR.Throw(false, "the chunk this allocToken points to does not exist.  either entity was deleted or it was packed.  Do not use AllocTokens beyond the frame aquired unless archetype.allocator.AutoPack==false");
			}
			return chunk;
		}
	}
	public Allocator GetAllocator()
	{
		return Allocator._GLOBAL_LOOKUP[allocatorId];
	}
	[Conditional("CHECKED")]
	private void _CHECKED_VerifyInstance<T>()
	{
		if (typeof(T) == typeof(AllocMetadata))
		{
			//otherwise can cause infinite recursion
			return;
		}
		ref readonly var allocMetadata = ref GetComponentReadRef<AllocMetadata>();
		__CHECKED.Throw(allocMetadata.allocToken == this, "mismatch");
		//get chunk via the allocator, where the default way is direct through the Chunk<T>._GLOBAL_LOOKUP
		var chunk = GetAllocator()._componentColumns[typeof(AllocMetadata)][allocSlot.columnChunkIndex] as Chunk<AllocMetadata>;
		__CHECKED.Throw(GetContainingChunk<AllocMetadata>() == chunk, "chunk lookup between both techniques does not match");
		__CHECKED.Throw(this == chunk.Span[allocSlot.chunkRowIndex].allocToken);
	}

	public int CompareTo(AllocToken other)
	{
		return allocSlot.CompareTo(other.allocSlot);
	}




	//public override string ToString()
	//{
	//	if (isInit)
	//	{
	//		return base.ToString();
	//	}
	//	else
	//	{
	//		return "AllocToken [NOT INITIALIZED]";
	//	}
	//}
}


[StructLayout(LayoutKind.Explicit)]
public record struct AllocSlot : IComparable<AllocSlot>
{

	[FieldOffset(0)]
	private long _packedValue;

	/// <summary>
	/// can be used to directly find a chunk from `Chunk[TComponent]._GLOBAL_LOOKUP(chunkId)`
	/// </summary>
	//[FieldOffset(0)]
	//public int chunkId;

	//[FieldOffset(0)]
	//public short allocatorId;
	/// <summary>
	/// index to the chunk from `allocator._componentColumns(type)[columnIndex]`
	/// </summary>


	[FieldOffset(0)]
	public int chunkRowIndex;
	[FieldOffset(4)]
	public int columnChunkIndex;

	public AllocSlot(//short allocatorId, 
		int columnIndex, int chunkRowIndex)
	{
		this = default;
		//this.allocatorId = allocatorId;
		this.columnChunkIndex = columnIndex;
		this.chunkRowIndex = chunkRowIndex;
	}

	public long GetChunkLookupId(int allocatorId)
	{
		//return (long)allocatorId << 32 | (uint)columnChunkIndex;
		return (long)allocatorId << 32 | (uint)columnChunkIndex;
	}

	public int CompareTo(AllocSlot other)
	{
		return _packedValue.CompareTo(other._packedValue);
	}

	public static bool operator <(AllocSlot left, AllocSlot right)
	{
		return left.CompareTo(right) < 0;
	}

	public static bool operator <=(AllocSlot left, AllocSlot right)
	{
		return left.CompareTo(right) <= 0;
	}

	public static bool operator >(AllocSlot left, AllocSlot right)
	{
		return left.CompareTo(right) > 0;
	}

	public static bool operator >=(AllocSlot left, AllocSlot right)
	{
		return left.CompareTo(right) >= 0;
	}
}

public partial class Allocator //unit test
{


	[Conditional("TEST")]
	public static unsafe void __TEST_Unit_ParallelAllocators()
	{
		//for (var i = 0; i < 1000; i++)
		//{
		//	Task.Run(() => __TEST_Unit_SingleAllocator());
		//}

		var result = Parallel.For(0, 10000, (index) => __TEST_Unit_SingleAllocator());
		__ERROR.Throw(result.IsCompleted);
	}
	[Conditional("TEST")]
	public static unsafe void __TEST_Unit_SeriallAllocators()
	{
		var count = 10000;
		var allocOwner = SpanOwner<Allocator>.Allocate(count);
		var allocs = allocOwner.Span;
		for (var i = 0; i < count; i++)
		{
			allocs[i] = _TEST_HELPER_CreateAllocator();
		}
		for (var i = 0; i < count; i++)
		{
			allocs[i].Dispose();
		}
		//var result = Parallel.For(0, 10000, (index) => __TEST_Unit_SingleAllocator());
		//__ERROR.Throw(result.IsCompleted);
	}

	[Conditional("TEST")]
	public static unsafe void __TEST_Unit_SingleAllocator()
	{
		var allocator = _TEST_HELPER_CreateAllocator();

		allocator.Dispose();
	}

	private static unsafe Allocator _TEST_HELPER_CreateAllocator()
	{
		var allocator = new Allocator()
		{
			AutoPack = __.Rand._NextBoolean(),
			ChunkSize = __.Rand.Next(1, 100),
			ComponentTypes = new() { typeof(int), typeof(string) },


		};
		allocator.Initialize();

		var externalIdsOwner = SpanOwner<long>.Allocate(__.Rand.Next(0, 1000));
		var set = new HashSet<long>();
		var externalIds = externalIdsOwner.Span;
		while (set.Count < externalIds.Length)
		{
			set.Add(__.Rand.NextInt64());
		}
		var count = 0;
		foreach (var id in set)
		{
			externalIds[count] = id;
			count++;
		}
		//Span<long> externalIds = stackalloc long[] { 2, 4, 8, 7, -2 };
		var tokensOwner = SpanOwner<AllocToken>.Allocate(externalIds.Length);
		var tokens = tokensOwner.Span;
		allocator.Alloc(externalIds, tokens);
		return allocator;
	}
}



/// <summary>
/// allocator for archetypes 
/// </summary>
public partial class Allocator : IDisposable //init logic
{

	public static int _allocatorId_GlobalCounter;
	public static Dictionary<int, Allocator> _GLOBAL_LOOKUP = new();
	public int _allocatorId = _allocatorId_GlobalCounter._InterlockedIncrement();

	/// <summary>
	/// if you want to add additional custom components to each entity, list them here.  These are not used to compute the <see cref="_componentsHashId"/>
	/// <para>be sure not to remove the items already in the list.</para>
	/// </summary>
	public List<Type> CustomMetaComponents = new List<Type>() { typeof(AllocMetadata) };


	public List<Type> ComponentTypes { get; init; }
	/// <summary>
	/// used to quickly identify what collection of ComponentTypes this allocator is in charge of
	/// </summary>
	public int _componentsHashId;

	public Dictionary<Type, List<Chunk>> _componentColumns = new();

	public bool HasComponent<T>()
	{
		return _componentColumns.ContainsKey(typeof(T));
	}

	public void Initialize()
	{
		//__DEBUG.AssertOnce(_allocatorId < 10, "//TODO: change allocatorId to use a pool, not increment, otherwise risk of collisions with long-running programs");
		__DEBUG.Throw(ComponentTypes != null, "need to set properties before init");
		//generate hash for fast matching of archetypes
		foreach (var type in ComponentTypes)
		{
			_componentsHashId += type.GetHashCode();
		}

		//create columns
		foreach (var type in ComponentTypes)
		{
			_componentColumns.Add(type, new());
		}
		//add our special metadata component column
		__DEBUG.Throw(CustomMetaComponents.Contains(typeof(AllocMetadata)), "we must have allocMetadata to store info on each entity added");
		foreach (var type in CustomMetaComponents)
		{
			_componentColumns.Add(type, new());
		}



		//create our next slot alloc tracker
		_nextSlotTracker = new()
		{
			chunkSize = ChunkSize,
			nextAvailable = new(0, 0),
		};

		//create the first (blank) chunk for each column
		_AllocNextChunk();

		lock (_GLOBAL_LOOKUP)
		{
			_GLOBAL_LOOKUP.Add(_allocatorId, this);
		}
	}
	public bool IsDisposed { get; private set; } = false;
	public void Dispose()
	{
		if (IsDisposed)
		{
			return;
		}
		IsDisposed = true;
		lock (_GLOBAL_LOOKUP)
		{
			_GLOBAL_LOOKUP.Remove(_allocatorId);
		}
		foreach (var (type, columnList) in _componentColumns)
		{
			foreach (var chunk in columnList)
			{
				chunk.Dispose();
			}
			columnList.Clear();
		}
		_componentColumns.Clear();
		_componentColumns = null;
		_free.Clear();
		_free = null;
		_lookup.Clear();
		_lookup = null;
	}
#if DEBUG
	~Allocator()
	{
		if (IsDisposed == false)
		{
			__DEBUG.AssertOnce(false, "need to have parent archetype dispose allocator for proper cleanup");
			Dispose();
		}
	}
#endif
}

public partial class Allocator //chunk management logic
{

	private void _AllocNextChunk() //TODO: preallocate extra chunks ahead of their need (always keep 1x extra chunk around)
	{
		foreach (var (type, columnList) in _componentColumns)
		{
			var chunkType = typeof(Chunk<>).MakeGenericType(type);
			var chunk = Activator.CreateInstance(chunkType) as Chunk;
			chunk.Initialize(_nextSlotTracker.chunkSize, _nextSlotTracker.nextAvailable.GetChunkLookupId(_allocatorId));
			__DEBUG.Assert(columnList.Count == _nextSlotTracker.nextAvailable.columnChunkIndex, "somehow our column allocations is out of step with our next free tracking.");
			columnList.Add(chunk);
		}
	}

	private void _FreeLastChunk()
	{
		foreach (var (type, columnList) in _componentColumns)
		{
			var result = columnList._TryTakeLast(out var chunk);
			__DEBUG.Throw(result && chunk._count == 0);
			chunk.Dispose();
			__DEBUG.Assert(columnList.Count == _nextSlotTracker.nextAvailable.columnChunkIndex, "somehow our column allocations is out of step with our next free tracking.");
		}
	}
}

public partial class Allocator  //alloc/free/pack logic
{
	/// <summary>
	/// when we pack, we move entities around.  This is used to determine if a AllocToken is out of date.
	/// </summary>
	public int _packVersion = 0;
	/// <summary>
	/// given an externalId, find current token.  this allows decoupling our internal storage location from external callers, allowing packing.
	/// </summary>
	public Dictionary<long, AllocToken> _lookup = new();


	/// <summary>
	/// temp listing of free entities, we will pack and/or deallocate at a specific time each frame
	/// </summary>
	public List<AllocSlot> _free = new();
	public bool _isFreeSorted = true;


	public int ChunkSize { get; init; } = 1000;
	public int Count { get => _lookup.Count; }
	/// <summary>
	/// the next slot we will allocate from, and logic informing us when to add/remove chunks
	/// </summary>
	public AllocPositionTracker _nextSlotTracker;



	/// <summary>
	/// default true, automatically pack when Free() is called. 
	/// </summary>
	public bool AutoPack { get; init; } = true;


	/// <summary>
	/// get a slot (recycling free if available)
	/// make allocToken
	/// add slot to columnList
	/// set builtin allocMetadata component
	/// verify
	/// </summary>
	public void Alloc(Span<long> externalIds, Span<AllocToken> output)
	{
		if (_isFreeSorted != true)
		{
			_free.Sort();
			_isFreeSorted = true;
		}

		__DEBUG.Assert(output.Length == externalIds.Length);
		for (var i = 0; i < externalIds.Length; i++)
		{
			var externalId = externalIds[i];
			//get next free.
			if (!_free._TryTakeLast(out var slot))
			{
				//  if we need to allocate a chunk do so here also.  //TODO: multithread
				slot = _nextSlotTracker.AllocNext(out var newChunk);
				if (newChunk)
				{
					_AllocNextChunk();
				}
			}
			ref var allocToken = ref output[i];
			allocToken = _GenerateLiveAllocToken(externalId, slot);
			//new()  
			//{
			//	isInit = true,
			//	allocatorId = _allocatorId,
			//	allocSlot = slot,
			//	externalId = externalId,
			//	packVersion = _packVersion,
			//};




			//loop all components zeroing out data and informing chunk of added item
			foreach (var (type, columnList) in _componentColumns)
			{
				columnList[slot.columnChunkIndex].OnAllocSlot(ref allocToken);
			}



			//set the allocMetadata builtin componenent
			ref var allocMetadata = ref allocToken.GetContainingChunk<AllocMetadata>().Span[allocToken.allocSlot.chunkRowIndex];
			__CHECKED.Throw(allocMetadata == default(AllocMetadata), "expect this to be cleared out, why not?");
			allocMetadata = new AllocMetadata()
			{
				allocToken = allocToken,
				componentCount = _componentColumns.Count,
			};
			__CHECKED.Throw(allocMetadata == allocToken.GetComponentReadRef<AllocMetadata>(), "component reference verification failed.  why?");


			//add to lookup
			_lookup.Add(externalId, allocToken);

			__CHECKED_VerifyAllocToken(ref allocToken);
		}


	}
	private AllocToken _GenerateLiveAllocToken(long externalId, AllocSlot slot)
	{
		return new()
		{
			isAlive = true,
			allocatorId = _allocatorId,
			allocSlot = slot,
			externalId = externalId,
			packVersion = _packVersion,
		};
	}

	private bool _TryQueryMetadata(AllocSlot slot, out AllocMetadata metadata)
	{
		var columnList = _componentColumns[typeof(AllocMetadata)];
		if (columnList.Count < slot.columnChunkIndex)
		{
			metadata = default;
			return false;
		}
		var chunk = columnList[slot.columnChunkIndex] as Chunk<AllocMetadata>;
		metadata = chunk.Span[slot.chunkRowIndex];
		return true;
	}


	[Conditional("CHECKED")]
	public void __CHECKED_VerifyAllocToken(ref AllocToken allocToken)
	{
		var storedToken = _lookup[allocToken.externalId];
		__ERROR.Throw(storedToken == allocToken);


		//make sure proper chunk is referenced, and field
		foreach (var (type, columnList) in _componentColumns)
		{
			var columnChunk = columnList[allocToken.allocSlot.columnChunkIndex];

			__CHECKED.Throw(columnChunk._chunkLookupId == allocToken.GetChunkLookupId(), "lookup id mismatch");

		}


		//verify chunk accessor workflows are correct
		var manualGetChunk = _componentColumns[typeof(AllocMetadata)][allocToken.allocSlot.columnChunkIndex] as Chunk<AllocMetadata>;
		var autoGetChunk = allocToken.GetContainingChunk<AllocMetadata>();
		__CHECKED.Throw(manualGetChunk == autoGetChunk, "should match");

		//verify allocMetadatas match
		__ERROR.Throw(manualGetChunk.Span[allocToken.allocSlot.chunkRowIndex].allocToken == allocToken);

	}



	/// <summary>
	/// get the allocTokens to delete
	/// verify
	/// delete from allocations lookup
	/// free slot from columnList
	/// add to free list
	/// if AutoPack, do it now.
	/// </summary>
	public unsafe void Free(Span<long> externalIds)
	{
		using var so_AllocTokens = SpanOwner<AllocToken>.Allocate(externalIds.Length);
		var allocTokens = so_AllocTokens.Span;
		//get tokens for freeing
		for (var i = 0; i < externalIds.Length; i++)
		{
			allocTokens[i] = _lookup[externalIds[i]];
			__CHECKED.Throw(allocTokens[i].externalId == externalIds[i]);
			__CHECKED_VerifyAllocToken(ref allocTokens[i]);
			//remove them now??  maybe will cause further issues with verification
			_lookup.Remove(externalIds[i]);
		}
		//sort so that when we itterate through, they will have a higher chance of being in the same chunk
		allocTokens.Sort();


		//parallel through all columns, deleting
		var allocTokensArraySegment = so_AllocTokens.DangerousGetArray();
		var allocArray = allocTokensArraySegment.Array;
		Parallel.ForEach(_componentColumns, (pair, loopState) =>
		{
			var (type, columnList) = pair;
			for (var i = 0; i < allocTokensArraySegment.Count; i++)
			{
				ref var allocToken = ref allocArray[i];
				columnList[allocToken.allocSlot.columnChunkIndex].OnFreeSlot(ref allocToken);
			}
		});



		//add to free list
		for (var i = 0; i < allocTokens.Length; i++)
		{
			__CHECKED.Throw(_free.Contains(allocTokens[i].allocSlot) == false);
			_free.Add(allocTokens[i].allocSlot);

		}
		_isFreeSorted = false;


		if (AutoPack == true)
		{
			var priorPackVersion = _packVersion;
			__DEBUG.Assert(externalIds.Length == _free.Count);
			Pack(externalIds.Length);
			__DEBUG.Assert(priorPackVersion != _packVersion && _free.Count == 0, "autopack not working?");
		}

	}


	private void _PackHelper_MoveSlotToFree(AllocToken highestAlive, AllocSlot lowestFree)
	{
		//verify freeSlot is free, and allocToken is valid
#if CHECKED
		__CHECKED_VerifyAllocToken(ref highestAlive);
		__CHECKED.Assert(highestAlive.allocSlot > lowestFree);
		if (!_TryQueryMetadata(lowestFree, out var freeSlotMeta))
		{
			__CHECKED.Throw(false, "this should not happen.  returning false means no chunk exists.");
		}
		__CHECKED.Assert(freeSlotMeta.allocToken.isAlive == false, "should be default value");
#endif
		//generate our newPos allocToken
		var newSlotAllocToken = _GenerateLiveAllocToken(highestAlive.externalId, lowestFree);

		//do a single alloc for that freeSlot componentColumns
		foreach (var (type, columnList) in _componentColumns)
		{
			//copy data from old while allocting the slot
			columnList[lowestFree.columnChunkIndex].OnPackSlot(ref newSlotAllocToken, ref highestAlive);
			//deallocate old slot componentColumns
			columnList[highestAlive.allocSlot.columnChunkIndex].OnFreeSlot(ref highestAlive);
		}
		//update the metadata component
		var metadataChunk = _componentColumns[typeof(AllocMetadata)][newSlotAllocToken.allocSlot.columnChunkIndex] as Chunk<AllocMetadata>;
		ref var metadataComponent = ref metadataChunk.Span[newSlotAllocToken.allocSlot.chunkRowIndex];
		metadataComponent.allocToken = newSlotAllocToken;

		//update our _lookup
		_lookup[newSlotAllocToken.externalId] = newSlotAllocToken;

		//make sure our newly moved is all setup properly
		__CHECKED_VerifyAllocToken(ref newSlotAllocToken);
	}

	public bool Pack(int maxCount)
	{
		//sor frees
		//loop through all to free, lowest to highest
		//if free is higher than highest active slot, done.
		//take highest active allocSlot and swap it with the free

		if (_free.Count == 0)
		{
			return false;
		}


		_packVersion++;



		var count = Math.Min(maxCount, _free.Count);


		if (_isFreeSorted != true)
		{
			_free.Sort();
			_isFreeSorted = true;
		}

		for (var i = 0; i < count; i++)
		{

			var firstFreeSlotToFill = _free[i];

			if (!_nextSlotTracker.TryGetHighestOccupiedSlot(out var highestFilled))
			{
				__ERROR.Assert(false, "investigate?  no slots are filled?  probably okay, just clear our slots?");
				break;
			}
			if (firstFreeSlotToFill >= highestFilled)
			{
				__ERROR.Assert(false, "investigate?  our free are higher than our filled?  probably okay, just clear our slots?");
				break;
			}
			if (!_TryQueryMetadata(highestFilled, out var highestAliveToken))
			{
				__ERROR.Throw(false);
			}
			//swap out free and highest
			_PackHelper_MoveSlotToFree(highestAliveToken.allocToken, firstFreeSlotToFill);



			//decrement our slotTracker position now that we have moved our top item			
			_nextSlotTracker.FreeLast(out var shouldFreeChunk);
#if CHECKED
			//verify next free is actually free
			var result = _TryQueryMetadata(_nextSlotTracker.nextAvailable, out var shouldBeFreeMetadata);
			__ERROR.Throw(result && shouldBeFreeMetadata.allocToken.isAlive == false);
#endif
			if (shouldFreeChunk)
			{
				_FreeLastChunk();
			}

		}
		//remove these free slots now that we are done filling them
		_free.RemoveRange(0, count);

#if CHECKED
		//verify that our highest allocated is either free or init
		if (_lookup.Count > 0)
		{
			var result = _nextSlotTracker.TryGetHighestOccupiedSlot(out var highestAllocated);
			__ERROR.Throw(result);
			result = _TryQueryMetadata(highestAllocated, out var highestMetadata);
			__CHECKED.Throw(highestMetadata.IsAlive || _free.Contains(highestAllocated));
		}
#endif

		return true;
	}



}
/// <summary>
/// data for a default chunk always applied to all allocators.
/// </summary>
public record struct AllocMetadata : IComponent
{
	public AllocToken allocToken;
	public int componentCount;
	/// <summary>
	/// hint informing that a writeRef was aquired for one of the components.
	/// <para>Important Note: writing to this fieldWrites is done internally, and does not increment the Chunk[AllocMetadata]._writeVersion.  This is so _writeVersion can be used to detect entity alloc/free </para>
	/// </summary>
	public int fieldWrites;

	/// <summary>
	/// If this slot is in use by an entity
	/// </summary>
	public bool IsAlive { get => allocToken.isAlive; }
}

/// <summary>
/// a helper struct that tracks and computes the next slot available.  when getting a new slot just check the value of `newChunk` to determine if a new chunk needs to be allocated.
/// </summary>
public struct AllocPositionTracker
{
	public int chunkSize;
	public AllocSlot nextAvailable;
	public AllocSlot AllocNext(out bool newChunk)
	{
		var toReturn = nextAvailable;
		nextAvailable.chunkRowIndex++;
		if (nextAvailable.chunkRowIndex >= chunkSize)
		{
			nextAvailable.chunkRowIndex = 0;
			nextAvailable.columnChunkIndex++;
			newChunk = true;
		}
		else
		{
			newChunk = false;
		}
		__CHECKED.Throw(nextAvailable != toReturn, "make sure these are still structs?");
		return toReturn;
	}
	public void FreeLast(out bool freeChunk)
	{
		//if(TryGetPriorSlot(nextAvailable,out var prior))
		//{
		//	if (nextAvailable.columnChunkIndex != prior.columnChunkIndex)
		//	{
		//		freeChunk = true;
		//	}
		//	else
		//	{
		//		freeChunk = false;
		//	}
		//	nextAvailable = prior;
		//}

		nextAvailable.chunkRowIndex--;
		if (nextAvailable.chunkRowIndex < 0)
		{
			nextAvailable.chunkRowIndex = chunkSize - 1;
			nextAvailable.columnChunkIndex--;
			freeChunk = true;
			__DEBUG.Throw(nextAvailable.columnChunkIndex >= 0, "less than zero allocations");
		}
		else
		{
			freeChunk = false;
		}
	}
	private bool TryGetPriorSlot(AllocSlot current, out AllocSlot prior)
	{
		if (nextAvailable.columnChunkIndex == 0 && nextAvailable.chunkRowIndex == 0)
		{
			prior = default;
			return false;
		}
		prior = current;
		prior.chunkRowIndex--;
		if (prior.chunkRowIndex < 0)
		{
			prior.chunkRowIndex = chunkSize - 1;
			prior.columnChunkIndex--;
		}
		return true;
	}
	/// <summary>
	/// false if no slots are allocated
	/// </summary>
	/// <param name="slot"></param>
	/// <returns></returns>
	internal bool TryGetHighestOccupiedSlot(out AllocSlot slot)
	{
		return TryGetPriorSlot(nextAvailable, out slot);
	}
}



public abstract class Chunk : IDisposable
{
	public long _chunkLookupId = -1;
	public int _count;
	public int _length = -1;
	/// <summary>
	/// incremented every time a system writes to any of its slots.  
	/// </summary>
	public int _writeVersion;



	/// <summary>
	/// delete from global chunk store
	/// </summary>
	public abstract void Dispose();

	/// <summary>
	/// using the given _chunkId, allocate self a slot on the global chunk store for Chunk[T]
	/// </summary>
	public abstract void Initialize(int length, long chunkLookupId);
	internal abstract void OnAllocSlot(ref AllocToken allocToken);
	/// <summary>
	/// overload used internally for packing
	/// </summary>
	internal abstract void OnPackSlot(ref AllocToken allocToken, ref AllocToken moveComponentDataFrom);
	internal abstract void OnFreeSlot(ref AllocToken allocToken);
}
[StructLayout(LayoutKind.Explicit)]
public record struct ChunkLookupId
{

	[FieldOffset(0)]
	public long _packedValue;
	[FieldOffset(0)]
	public int columnChunkIndex;
	[FieldOffset(4)]
	public int allocatorId;
}


public class Chunk<TComponent> : Chunk
{
	public static Dictionary<long, Chunk<TComponent>> _GLOBAL_LOOKUP = new(100000);

	private MemoryOwner<TComponent> _storage;
	public Memory<TComponent> Memory { get => _storage.Memory; }
	public Span<TComponent> Span { get => _storage.Span; }

	/// <summary>
	/// this is an array obtained by a object pool (cache).  It is longer than actually needed.  Do not use the extra slots.  always get length from _storage or Span
	/// </summary>
	private TComponent[] _DANGEROUS_refStorage;
	public bool IsDisposed { get; private set; } = false;
	public override void Dispose()
	{
		IsDisposed = true;
		lock (_GLOBAL_LOOKUP)
		{
			var result = _GLOBAL_LOOKUP._TryRemove(_chunkLookupId, out _);
			__ERROR.Throw(result);
		}
		//our allocator.FreeLastChunk code checks count.   we don't want to check count here because of cases like game shutdown
		//__ERROR.Throw(_count == 0); 
		_DANGEROUS_refStorage = null;
		_storage.Dispose();
		_storage = null;
	}

	public override void Initialize(int length, long chunkLookupId)
	{
		_chunkLookupId = chunkLookupId;
		_length = length;

		__DEBUG.Throw(_chunkLookupId != -1 && _length != -1, "need to set before init");
		lock (_GLOBAL_LOOKUP)
		{
			var result = _GLOBAL_LOOKUP.TryAdd(_chunkLookupId, this);
			__ERROR.Throw(result);
		}

		_storage = MemoryOwner<TComponent>.Allocate(_length, AllocationMode.Clear); //TODO: maybe no need to clear?
		_DANGEROUS_refStorage = _storage.DangerousGetArray().Array;
	}
	internal override void OnAllocSlot(ref AllocToken allocToken)
	{
		_count++;
#if DEBUG
		//clear the slot
		_DANGEROUS_refStorage[allocToken.allocSlot.chunkRowIndex] = default(TComponent);
#endif
	}
	/// <summary>
	/// overload used internally for packing
	/// </summary>
	internal override void OnPackSlot(ref AllocToken allocToken, ref AllocToken moveComponentDataFrom)
	{
		_count++;
		_DANGEROUS_refStorage[allocToken.allocSlot.chunkRowIndex] = moveComponentDataFrom.GetComponentReadRef<TComponent>();
#if DEBUG
		lock (_GLOBAL_LOOKUP)
		{
			//clear the old slot.  this isn't needed, but in case someone is still using the old ref, lets make them aware of it in DEBUG
			_GLOBAL_LOOKUP[moveComponentDataFrom.GetChunkLookupId()]._DANGEROUS_refStorage[moveComponentDataFrom.allocSlot.chunkRowIndex] = default(TComponent);
		}
#endif

	}
	internal override void OnFreeSlot(ref AllocToken allocToken)
	{
		_count--;

		//if (allocToken.GetAllocator().AutoPack)
		//{
		//	//no need to clear, as we will pack over this!
		//	return;
		//}

		//clear the slot
		_DANGEROUS_refStorage[allocToken.allocSlot.chunkRowIndex] = default(TComponent);
	}
	public unsafe ref TComponent GetWriteRef(AllocToken allocToken)
	{
		return ref GetWriteRef(ref *&allocToken); //cast to ptr using *& to circumvent return ref safety check
	}
	public ref TComponent GetWriteRef(ref AllocToken allocToken)
	{
		lock (Chunk<AllocMetadata>._GLOBAL_LOOKUP)
		{
			_CHECKED_VerifyIntegrity(ref allocToken);
			var rowIndex = allocToken.allocSlot.chunkRowIndex;
			//inform metadata that a write is occuring.  //TODO: is this needed?  If not, remove it to reduce random memory access
			ref var allocMetadata = ref Chunk<AllocMetadata>._GLOBAL_LOOKUP[_chunkLookupId]._DANGEROUS_refStorage[rowIndex];
			allocMetadata.fieldWrites++;
			_writeVersion++;
			return ref _DANGEROUS_refStorage[rowIndex];
		}
	}
	public unsafe ref readonly TComponent GetReadRef(AllocToken allocToken)
	{
		return ref GetReadRef(ref *&allocToken); //cast to ptr using *& to circumvent return ref safety check
	}
	public ref readonly TComponent GetReadRef(ref AllocToken allocToken)
	{
		_CHECKED_VerifyIntegrity(ref allocToken);
		var rowIndex = allocToken.allocSlot.chunkRowIndex;
		return ref _DANGEROUS_refStorage[rowIndex];

	}

	[Conditional("CHECKED")]
	private void _CHECKED_VerifyIntegrity(ref AllocToken allocToken)
	{
		lock (_GLOBAL_LOOKUP)
		{
			__DEBUG.Throw(allocToken.GetChunkLookupId() == _chunkLookupId, "allocToken does not belong to this chunk");
			__CHECKED.Throw(Chunk<TComponent>._GLOBAL_LOOKUP[_chunkLookupId] == this, "alloc system internal integrity failure");
			__CHECKED.Throw(!IsDisposed, "use after dispose");
			var rowIndex = allocToken.allocSlot.chunkRowIndex;
			ref var allocMetadata = ref Chunk<AllocMetadata>._GLOBAL_LOOKUP[_chunkLookupId]._DANGEROUS_refStorage[rowIndex];
			__DEBUG.Throw(allocMetadata.allocToken == allocToken, "invalid alloc token.   why?");
		}
	}
}


public class AllocSlotList<T>
{
	private List<T> _storage = new();
	public int _count;

	/// <summary>
	/// get storage as a span.  reads are safe with AllocSlot() as long as slots being added are not access via this Span.
	/// </summary>
	public Span<T> Span
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_storage);
	}

	private List<int> _freeSlots = new();

	public int AllocSlot()
	{
		int slot;
		bool result;
		lock (_freeSlots)
		{
			result = _freeSlots._TryTakeLast(out slot);
		}
		if (!result)
		{
			lock (_storage)
			{
				slot = _storage.Count;
				_storage.Add(default(T));
				_count++;
			}
		}
		return slot;
	}

	public void FreeSlot(int slot)
	{
		lock (_freeSlots)
		{
			_freeSlots.Add(slot);

		}
		lock (_storage)
		{
			_count--;
			_storage[slot] = default(T);

			//try to pack if possible
			if (slot == _storage.Count - 1)
			{
				//the slot we are freeing is the last slot in the _storage array.    
				lock (_freeSlots)
				{
					//now have exclusive lock on _freeSlots and _storage

					//sort free so highest at end
					_freeSlots.Sort();

					//while the last free slot is the last slot in storage, remove both
					while (_freeSlots[_freeSlots.Count - 1] == _storage.Count - 1)
					{

						var result = _freeSlots._TryTakeLast(out var removedFreeSlot);
						__DEBUG.Throw(result && removedFreeSlot == _storage.Count - 1);
						_storage._RemoveLast();
					}
				}
			}
		}
	}
}