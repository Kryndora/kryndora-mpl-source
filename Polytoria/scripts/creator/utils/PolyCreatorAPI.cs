// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Schemas.API;
using Polytoria.Shared;
using Polytoria.Utils;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace Polytoria.Creator.Utils;

public static class PolyCreatorAPI
{
	private static readonly PTHttpClient _client = new();

	public static int UserID { get; private set; } = 0;
	public static APIUserInfo UserInfo { get; private set; }
	public static string Token { get; private set; } = "";

	public static event Action<int>? LaunchPlaceRequest;
	public static event Action? UserAuthenticated;
	public static bool IsUserAuthenticated { get; private set; }

	private static string ApiUrl(string path)
	{
		return Globals.ApiEndpoint.TrimEnd('/') + "/" + path.TrimStart('/');
	}

	private static string MainUrl(string path)
	{
		return Globals.MainEndpoint.TrimEnd('/') + "/" + path.TrimStart('/');
	}

	public static void SetToken(string token)
	{
		Token = token;
		_client.DefaultRequestHeaders["Authorization"] = token;
		PolyAPI.SetAuthToken(token);
	}

	private static void EnsureLocalAuthentication()
	{
		if (IsUserAuthenticated) return;
	}

	public static async Task LoginWithToken(string token)
	{
		SetToken(token);
		CreatorAuthResponse res = await _client.GetFromJsonAsync(ApiUrl("/v1/creator/token-data"), CreatorAPIGenerationContext.Default.CreatorAuthResponse);
		if (res.UserID <= 0)
			throw new AuthenticationException("Creator authentication required");

		if (!string.IsNullOrWhiteSpace(res.Token) && res.Token != token)
			SetToken(res.Token);

		UserID = res.UserID;
		if (res.PlaceID.HasValue)
		{
			LaunchPlaceRequest?.Invoke(res.PlaceID.Value);
		}

		UserInfo = await PolyAPI.GetUserFromID(UserID);
		IsUserAuthenticated = true;
		UserAuthenticated?.Invoke();
	}

	public static async Task<bool> TryLoginFromLocalSession()
	{
		try
		{
			CreatorLocalSessionResponse res = await _client.GetFromJsonAsync(MainUrl("/api/local/creator/session"), CreatorAPIGenerationContext.Default.CreatorLocalSessionResponse);
			if (!res.Ok || string.IsNullOrWhiteSpace(res.Token))
				return false;

			await LoginWithToken(res.Token);
			return IsUserAuthenticated;
		}
		catch
		{
			return false;
		}
	}

	public static async Task<CreatorPlaceItem[]> GetPublishedWorlds()
	{
		EnsureLocalAuthentication();
		if (!IsUserAuthenticated) throw new AuthenticationException("User authentication required");
		using HttpResponseMessage msg = await _client.GetAsync(ApiUrl("/v1/creator/get-places"));
		msg.EnsureSuccessStatusCode();
		return await msg.Content.ReadFromJsonAsync(CreatorAPIGenerationContext.Default.CreatorPlaceItemArray) ?? [];
	}

	public static async Task<CreatorPublishResponse> UploadWorld(byte[] placeData, int placeID = 0, string mainWorldPath = "")
	{
		EnsureLocalAuthentication();
		if (!IsUserAuthenticated) throw new AuthenticationException("User authentication required");
		using MultipartFormDataContent form = new()
		{
			{ new StringContent(placeID.ToString()), "id" },
			{ new StringContent(Token), "token" },
			{ new StringContent(mainWorldPath), "mainPlacePath" },
			{ new StringContent(Globals.MajorAppVersion), "majorVersion" },
		};

		ByteArrayContent fileContent = new(placeData);
		fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
		form.Add(fileContent, "file", "level.ptpacked");

		using HttpResponseMessage msg = await _client.PostAsync(ApiUrl("/v1/creator/upload-place"), form);
		msg.EnsureSuccessStatusCode();
		return await msg.Content.ReadFromJsonAsync(CreatorAPIGenerationContext.Default.CreatorPublishResponse);
	}

	public static async Task<CreatorPublishResponse> UploadModel(byte[] modelData, int modelId = 0)
	{
		EnsureLocalAuthentication();
		if (!IsUserAuthenticated) throw new AuthenticationException("User authentication required");
		using MultipartFormDataContent form = new()
		{
			{ new StringContent(modelId.ToString()), "id" },
			{ new StringContent(Token), "token" },
			{ new StringContent(Globals.MajorAppVersion), "majorVersion" },
		};

		ByteArrayContent fileContent = new(modelData);
		fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
		form.Add(fileContent, "data", "model.ptmd");

		using HttpResponseMessage msg = await _client.PostAsync(ApiUrl("/v1/creator/upload-model"), form);
		msg.EnsureSuccessStatusCode();
		return await msg.Content.ReadFromJsonAsync(CreatorAPIGenerationContext.Default.CreatorPublishResponse);
	}
}
