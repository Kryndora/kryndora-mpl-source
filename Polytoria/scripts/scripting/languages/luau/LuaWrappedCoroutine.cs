// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Threading.Tasks;

namespace Polytoria.Scripting.Luau;

public class LuaWrappedCoroutine : LuaObject
{
	public int ThreadRef;

	public int WrapCall(IntPtr L)
	{
		LuaState state = LuaState.FromIntPtr(L);
		state.GetRef(ThreadRef);
		LuaState thread = state.ToThread(-1);
		state.Pop(1);

		int totalArgs = state.GetTop();
		int nargs = totalArgs - 1; // Exclude the userdata

		state.Remove(1); // Remove userdata

		state.XMove(thread, nargs);

		_ = HandleWrapCallAsync(state, thread, nargs);
		return 0;
	}

	private async Task HandleWrapCallAsync(LuaState state, LuaState thread, int nargs)
	{
		await LuauProvider.ResumeThread(thread, state, nargs);
		state.Unref(ThreadRef);
	}
}
