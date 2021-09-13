﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DumDum.Bcl;


public static class zz__Extensions_Array
{


	public static float _SUM(ref this Span<float> target)
	{
		var total = 0f;
		for (var i = 0; i < target.Length; i++)
		{
			total += target[i];
		}

		return total;
	}
	public static float _AVG(ref this Span<float> target)
	{

		var total = target._SUM();
		return total / target.Length;
	}
	public static float _MAX(ref this Span<float> target)
	{
		if (target.Length == 0)
		{
			return float.NaN;
		}

		var max = float.NegativeInfinity;
		for (var i = 0; i < target.Length; i++)
		{
			max = target[i] > max ? target[i] : max;
		}

		return max;
	}
	public static float _MIN(ref this Span<float> target)
	{
		if (target.Length == 0)
		{
			return float.NaN;
		}

		var min = float.PositiveInfinity;
		for (var i = 0; i < target.Length; i++)
		{
			min = target[i] < min ? target[i] : min;
		}

		return min;
	}

	public static float _SUM(this float[] target)
	{
		var total = 0f;
		for (var i = 0; i < target.Length; i++)
		{
			total += target[i];
		}

		return total;
	}
	public static float _AVG(this float[] target)
	{

		var total = target._SUM();
		return total / target.Length;
	}
	public static float _MAX(this float[] target)
	{
		if (target.Length == 0)
		{
			return float.NaN;
		}

		var max = float.NegativeInfinity;
		for (var i = 0; i < target.Length; i++)
		{
			max = target[i] > max ? target[i] : max;
		}

		return max;
	}
	public static float _MIN(this float[] target)
	{
		if (target.Length == 0)
		{
			return float.NaN;
		}

		var min = float.PositiveInfinity;
		for (var i = 0; i < target.Length; i++)
		{
			min = target[i] < min ? target[i] : min;
		}

		return min;
	}
}

public static class zz__Extensions_List
{
	private static Random _rand = new();

	public static bool _TryRemoveRandom<T>(this List<T> target, out T value)
	{
		if (target.Count == 0)
		{
			value = default;
			return false;
		}
		var index = -1;
		lock (_rand)
		{
			index = _rand.Next(0, target.Count);
		}

		value = target[index];
		target.RemoveAt(index);
		return true;
	}
}

/// <summary>Extension methods for <see cref="TaskCompletionSource{TResult}"/>.</summary>
/// <threadsafety static="true" instance="false"/>
/// <remarks>from: https://github.com/tunnelvisionlabs/dotnet-threading/blob/3e99a9d13476a1e8224d81f282f3cedad143c1bc/Rackspace.Threading/TaskCompletionSourceExtensions.cs</remarks>
public static class zz_Extensions_TaskCompletionSource
{
	/// <summary>Transfers the result of a <see cref="Task{TResult}"/> to a <see cref="TaskCompletionSource{TResult}"/>.</summary>
	/// <remarks>
	/// <para>If <paramref name="task"/> is in the <see cref="TaskStatus.RanToCompletion"/> state,
	/// the result of the task is assigned to the <see cref="TaskCompletionSource{TResult}"/>
	/// using the <see cref="TaskCompletionSource{TResult}.SetResult(TResult)"/> method.</para>
	///
	/// <para>If <paramref name="task"/> is in the <see cref="TaskStatus.Faulted"/> state,
	/// the unwrapped exceptions are bound to the <see cref="TaskCompletionSource{TResult}"/>
	/// using the <see cref="TaskCompletionSource{TResult}.SetException(IEnumerable{Exception})"/>
	/// method.</para>
	///
	/// <para>If <paramref name="task"/> is in the <see cref="TaskStatus.Canceled"/> state,
	/// the <see cref="TaskCompletionSource{TResult}"/> is transitioned to the
	/// <see cref="TaskStatus.Canceled"/> state using the
	/// <see cref="TaskCompletionSource{TResult}.SetCanceled"/> method.</para>
	/// </remarks>
	/// <typeparam name="TSource">Specifies the result type of the source <see cref="Task{TResult}"/>.</typeparam>
	/// <typeparam name="TResult">Specifies the result type of the <see cref="TaskCompletionSource{TResult}"/>.</typeparam>
	/// <param name="taskCompletionSource">The <see cref="TaskCompletionSource{TResult}"/> instance.</param>
	/// <param name="task">The result task whose completion results should be transferred.</param>
	/// <exception cref="ArgumentNullException">
	/// <para>If <paramref name="taskCompletionSource"/> is <see langword="null"/>.</para>
	/// <para>-or-</para>
	/// <para>If <paramref name="task"/> is <see langword="null"/>.</para>
	/// </exception>
	/// <exception cref="ObjectDisposedException">
	/// <para>If the underlying <see cref="Task{TResult}"/> of <paramref name="taskCompletionSource"/> was disposed.</para>
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// <para>If the underlying <see cref="Task{TResult}"/> produced by <paramref name="taskCompletionSource"/> is already
	/// in one of the three final states: <see cref="TaskStatus.RanToCompletion"/>,
	/// <see cref="TaskStatus.Faulted"/>, or <see cref="TaskStatus.Canceled"/>.</para>
	/// </exception>
	public static void SetFromTask<TSource, TResult>(this TaskCompletionSource<TResult> taskCompletionSource, Task<TSource> task)
		where TSource : TResult
	{
		if (taskCompletionSource == null)
			throw new ArgumentNullException("taskCompletionSource");
		if (task == null)
			throw new ArgumentNullException("task");

		switch (task.Status)
		{
			case TaskStatus.RanToCompletion:
				taskCompletionSource.SetResult(task.Result);
				break;

			case TaskStatus.Faulted:
				taskCompletionSource.SetException(task.Exception.InnerExceptions);
				break;

			case TaskStatus.Canceled:
				taskCompletionSource.SetCanceled();
				break;

			default:
				throw new InvalidOperationException("The task was not completed.");
		}
	}
	public static void SetFromTask(this TaskCompletionSource taskCompletionSource, Task task)
	{
		if (taskCompletionSource == null)
			throw new ArgumentNullException("taskCompletionSource");
		if (task == null)
			throw new ArgumentNullException("task");

		switch (task.Status)
		{
			case TaskStatus.RanToCompletion:
				taskCompletionSource.SetResult();
				break;

			case TaskStatus.Faulted:
				taskCompletionSource.SetException(task.Exception.InnerExceptions);
				break;

			case TaskStatus.Canceled:
				taskCompletionSource.SetCanceled();
				break;

			default:
				throw new InvalidOperationException("The task was not completed.");
		}
	}

	/// <summary>
	/// Transfers the result of a <see cref="Task{TResult}"/> to a <see cref="TaskCompletionSource{TResult}"/>,
	/// using a specified result value when the task is in the <see cref="TaskStatus.RanToCompletion"/>
	/// state.
	/// </summary>
	/// <remarks>
	/// <para>If <paramref name="task"/> is in the <see cref="TaskStatus.RanToCompletion"/> state,
	/// the specified <paramref name="result"/> value is assigned to the
	/// <see cref="TaskCompletionSource{TResult}"/> using the
	/// <see cref="TaskCompletionSource{TResult}.SetResult(TResult)"/> method.</para>
	///
	/// <para>If <paramref name="task"/> is in the <see cref="TaskStatus.Faulted"/> state,
	/// the unwrapped exceptions are bound to the <see cref="TaskCompletionSource{TResult}"/>
	/// using the <see cref="TaskCompletionSource{TResult}.SetException(IEnumerable{Exception})"/>
	/// method.</para>
	///
	/// <para>If <paramref name="task"/> is in the <see cref="TaskStatus.Canceled"/> state,
	/// the <see cref="TaskCompletionSource{TResult}"/> is transitioned to the
	/// <see cref="TaskStatus.Canceled"/> state using the
	/// <see cref="TaskCompletionSource{TResult}.SetCanceled"/> method.</para>
	/// </remarks>
	/// <typeparam name="TResult">Specifies the result type of the <see cref="TaskCompletionSource{TResult}"/>.</typeparam>
	/// <param name="taskCompletionSource">The <see cref="TaskCompletionSource{TResult}"/> instance.</param>
	/// <param name="task">The result task whose completion results should be transferred.</param>
	/// <param name="result">The result of the completion source when the specified task completed successfully.</param>
	/// <exception cref="ArgumentNullException">
	/// <para>If <paramref name="taskCompletionSource"/> is <see langword="null"/>.</para>
	/// <para>-or-</para>
	/// <para>If <paramref name="task"/> is <see langword="null"/>.</para>
	/// </exception>
	/// <exception cref="ObjectDisposedException">
	/// <para>If the underlying <see cref="Task{TResult}"/> of <paramref name="taskCompletionSource"/> was disposed.</para>
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// <para>If the underlying <see cref="Task{TResult}"/> produced by <paramref name="taskCompletionSource"/> is already
	/// in one of the three final states: <see cref="TaskStatus.RanToCompletion"/>,
	/// <see cref="TaskStatus.Faulted"/>, or <see cref="TaskStatus.Canceled"/>.</para>
	/// </exception>
	public static void SetFromTask<TResult>(this TaskCompletionSource<TResult> taskCompletionSource, Task task, TResult result)
	{
		switch (task.Status)
		{
			case TaskStatus.RanToCompletion:
				taskCompletionSource.SetResult(result);
				break;

			case TaskStatus.Faulted:
				taskCompletionSource.SetException(task.Exception.InnerExceptions);
				break;

			case TaskStatus.Canceled:
				taskCompletionSource.SetCanceled();
				break;

			default:
				throw new InvalidOperationException("The task was not completed.");
		}
	}

	/// <summary>Attempts to transfer the result of a <see cref="Task{TResult}"/> to a <see cref="TaskCompletionSource{TResult}"/>.</summary>
	/// <remarks>
	/// <para>This method will return <see langword="false"/> if the <see cref="Task{TResult}"/>
	/// provided by <paramref name="taskCompletionSource"/> is already in one of the three
	/// final states: <see cref="TaskStatus.RanToCompletion"/>, <see cref="TaskStatus.Faulted"/>,
	/// or <see cref="TaskStatus.Canceled"/>. This method also returns <see langword="false"/>
	/// if the underlying <see cref="Task{TResult}"/> has already been disposed.</para>
	///
	/// <para>If <paramref name="task"/> is in the <see cref="TaskStatus.RanToCompletion"/> state,
	/// the result of the task is assigned to the <see cref="TaskCompletionSource{TResult}"/>
	/// using the <see cref="TaskCompletionSource{TResult}.TrySetResult(TResult)"/> method.</para>
	///
	/// <para>If <paramref name="task"/> is in the <see cref="TaskStatus.Faulted"/> state,
	/// the unwrapped exceptions are bound to the <see cref="TaskCompletionSource{TResult}"/>
	/// using the <see cref="TaskCompletionSource{TResult}.TrySetException(IEnumerable{Exception})"/>
	/// method.</para>
	///
	/// <para>If <paramref name="task"/> is in the <see cref="TaskStatus.Canceled"/> state,
	/// the <see cref="TaskCompletionSource{TResult}"/> is transitioned to the
	/// <see cref="TaskStatus.Canceled"/> state using the
	/// <see cref="TaskCompletionSource{TResult}.TrySetCanceled"/> method.</para>
	/// </remarks>
	/// <typeparam name="TSource">Specifies the result type of the source <see cref="Task{TResult}"/>.</typeparam>
	/// <typeparam name="TResult">Specifies the result type of the <see cref="TaskCompletionSource{TResult}"/>.</typeparam>
	/// <param name="taskCompletionSource">The <see cref="TaskCompletionSource{TResult}"/> instance.</param>
	/// <param name="task">The result task whose completion results should be transferred.</param>
	/// <returns>
	/// <para><see langword="true"/> if the operation was successful.</para>
	/// <para>-or-</para>
	/// <para><see langword="false"/> if the operation was unsuccessful or the object has already been disposed.</para>
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// <para>If <paramref name="taskCompletionSource"/> is <see langword="null"/>.</para>
	/// <para>-or-</para>
	/// <para>If <paramref name="task"/> is <see langword="null"/>.</para>
	/// </exception>
	public static bool TrySetFromTask<TSource, TResult>(this TaskCompletionSource<TResult> taskCompletionSource, Task<TSource> task)
		where TSource : TResult
	{
		switch (task.Status)
		{
			case TaskStatus.RanToCompletion:
				return taskCompletionSource.TrySetResult(task.Result);

			case TaskStatus.Faulted:
				return taskCompletionSource.TrySetException(task.Exception.InnerExceptions);

			case TaskStatus.Canceled:
				return taskCompletionSource.TrySetCanceled();

			default:
				throw new InvalidOperationException("The task was not completed.");
		}
	}
	public static bool TrySetFromTask(this TaskCompletionSource taskCompletionSource, Task task)
	{
		switch (task.Status)
		{
			case TaskStatus.RanToCompletion:
				return taskCompletionSource.TrySetResult();

			case TaskStatus.Faulted:
				return taskCompletionSource.TrySetException(task.Exception.InnerExceptions);

			case TaskStatus.Canceled:
				return taskCompletionSource.TrySetCanceled();

			default:
				throw new InvalidOperationException("The task was not completed.");
		}
	}

	/// <summary>
	/// Attempts to transfer the result of a <see cref="Task{TResult}"/> to a <see cref="TaskCompletionSource{TResult}"/>,
	/// using a specified result value when the task is in the <see cref="TaskStatus.RanToCompletion"/>
	/// state.
	/// </summary>
	/// <remarks>
	/// <para>This method will return <see langword="false"/> if the <see cref="Task{TResult}"/>
	/// provided by <paramref name="taskCompletionSource"/> is already in one of the three
	/// final states: <see cref="TaskStatus.RanToCompletion"/>, <see cref="TaskStatus.Faulted"/>,
	/// or <see cref="TaskStatus.Canceled"/>. This method also returns <see langword="false"/>
	/// if the underlying <see cref="Task{TResult}"/> has already been disposed.</para>
	///
	/// <para>If <paramref name="task"/> is in the <see cref="TaskStatus.RanToCompletion"/> state,
	/// the specified <paramref name="result"/> value is assigned to the
	/// <see cref="TaskCompletionSource{TResult}"/> using the
	/// <see cref="TaskCompletionSource{TResult}.TrySetResult(TResult)"/> method.</para>
	///
	/// <para>If <paramref name="task"/> is in the <see cref="TaskStatus.Faulted"/> state,
	/// the unwrapped exceptions are bound to the <see cref="TaskCompletionSource{TResult}"/>
	/// using the <see cref="TaskCompletionSource{TResult}.TrySetException(IEnumerable{Exception})"/>
	/// method.</para>
	///
	/// <para>If <paramref name="task"/> is in the <see cref="TaskStatus.Canceled"/> state,
	/// the <see cref="TaskCompletionSource{TResult}"/> is transitioned to the
	/// <see cref="TaskStatus.Canceled"/> state using the
	/// <see cref="TaskCompletionSource{TResult}.TrySetCanceled"/> method.</para>
	/// </remarks>
	/// <typeparam name="TResult">Specifies the result type of the <see cref="TaskCompletionSource{TResult}"/>.</typeparam>
	/// <param name="taskCompletionSource">The <see cref="TaskCompletionSource{TResult}"/> instance.</param>
	/// <param name="task">The result task whose completion results should be transferred.</param>
	/// <param name="result">The result of the completion source when the specified task completed successfully.</param>
	/// <returns>
	/// <para><see langword="true"/> if the operation was successful.</para>
	/// <para>-or-</para>
	/// <para><see langword="false"/> if the operation was unsuccessful or the object has already been disposed.</para>
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// <para>If <paramref name="taskCompletionSource"/> is <see langword="null"/>.</para>
	/// <para>-or-</para>
	/// <para>If <paramref name="task"/> is <see langword="null"/>.</para>
	/// </exception>
	public static bool TrySetFromTask<TResult>(this TaskCompletionSource<TResult> taskCompletionSource, Task task, TResult result)
	{
		switch (task.Status)
		{
			case TaskStatus.RanToCompletion:
				return taskCompletionSource.TrySetResult(result);

			case TaskStatus.Faulted:
				return taskCompletionSource.TrySetException(task.Exception.InnerExceptions);

			case TaskStatus.Canceled:
				return taskCompletionSource.TrySetCanceled();

			default:
				throw new InvalidOperationException("The task was not completed.");
		}
	}

	/// <summary>Transfers the result of a canceled or faulted <see cref="Task"/> to the <see cref="TaskCompletionSource{TResult}"/>.</summary>
	/// <typeparam name="TResult">Specifies the type of the result.</typeparam>
	/// <param name="taskCompletionSource">The TaskCompletionSource.</param>
	/// <param name="task">The task whose completion results should be transferred.</param>
	public static void SetFromFailedTask<TResult>(this TaskCompletionSource<TResult> taskCompletionSource, Task task)
	{
		switch (task.Status)
		{
			case TaskStatus.Faulted:
				taskCompletionSource.SetException(task.Exception.InnerExceptions);
				break;

			case TaskStatus.Canceled:
				taskCompletionSource.SetCanceled();
				break;

			case TaskStatus.RanToCompletion:
				throw new InvalidOperationException("Failed tasks must be in the Canceled or Faulted state.");

			default:
				throw new InvalidOperationException("The task was not completed.");
		}
	}
	public static void SetFromFailedTask(this TaskCompletionSource taskCompletionSource, Task task)
	{
		switch (task.Status)
		{
			case TaskStatus.Faulted:
				taskCompletionSource.SetException(task.Exception.InnerExceptions);
				break;

			case TaskStatus.Canceled:
				taskCompletionSource.SetCanceled();
				break;

			case TaskStatus.RanToCompletion:
				throw new InvalidOperationException("Failed tasks must be in the Canceled or Faulted state.");

			default:
				throw new InvalidOperationException("The task was not completed.");
		}
	}
}
