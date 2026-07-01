// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Schemas.API;
using Polytoria.Shared;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Polytoria.Utils;

public static class PolyAPI
{
	private static readonly PTHttpClient _client = new();

	private static string ApiUrl(string path)
	{
		return Globals.ApiEndpoint.TrimEnd('/') + "/" + path.TrimStart('/');
	}

	private static string MainUrl(string path)
	{
		return Globals.MainEndpoint.TrimEnd('/') + "/" + path.TrimStart('/');
	}

	public static void SetAuthToken(string userToken)
	{
		// Remove Authorization if exists
		_client.DefaultRequestHeaders.Remove("Authorization");
		_client.DefaultRequestHeaders.Add("Authorization", "Bearer " + userToken);
	}

	public static Task<APIUserInfo> GetUserFromID(int userID)
	{
		return _client.GetFromJsonAsync(
			ApiUrl("/v1/users/" + userID.ToString()),
			APIGenerationContext.Default.APIUserInfo
		);
	}

	public static Task<APIMeResponse> GetCurrentUser()
	{
		return _client.GetFromJsonAsync(
			MainUrl("/api/users/me"),
			APIGenerationContext.Default.APIMeResponse
		);
	}

	public static Task<APIMeResponse> GetCurrentUser(string userToken)
	{
		PTHttpClient client = new();
		client.DefaultRequestHeaders.Add("Authorization", "Bearer " + userToken);
		return client.GetFromJsonAsync(
			MainUrl("/api/users/me"),
			APIGenerationContext.Default.APIMeResponse
		);
	}

	public static async Task<APIJoinPlaceResponse> RequestJoinGame(APIJoinPlaceRequest req)
	{
		HttpResponseMessage response = await _client.PostAsJsonAsync(
			MainUrl("/api/places/join"),
			req,
			APIGenerationContext.Default.APIJoinPlaceRequest
		);

		response.EnsureSuccessStatusCode();

		APIJoinPlaceResponse result = await response.Content.ReadFromJsonAsync(
			APIGenerationContext.Default.APIJoinPlaceResponse
		);

		return result;
	}

	public static Task<APIAvatarResponse> GetUserAvatarFromID(int userID)
	{
		return _client.GetFromJsonAsync(
			ApiUrl("/v1/users/" + userID.ToString() + "/avatar"),
			APIGenerationContext.Default.APIAvatarResponse
		);
	}

	public static Task<APIPlaceInfo> GetWorldFromID(int placeID)
	{
		return _client.GetFromJsonAsync(
			ApiUrl("/v1/places/" + placeID.ToString()),
			APIGenerationContext.Default.APIPlaceInfo
		);
	}

	public static Task<APIPlaceMedia[]?> GetWorldMedia(int placeID)
	{
		return _client.GetFromJsonAsync(
			ApiUrl("/v1/places/" + placeID.ToString() + "/media"),
			APIGenerationContext.Default.APIPlaceMediaArray
		);
	}

	public static Task<APIFeedPostRoot> GetFeedPosts(int page = 1)
	{
		return _client.GetFromJsonAsync(
			MainUrl("/api/feed?page=" + page.ToString()),
			APIGenerationContext.Default.APIFeedPostRoot
		);
	}

	public static Task<APIWorldsRoot> GetWorlds()
	{
		return _client.GetFromJsonAsync(
			MainUrl("/api/places"),
			APIGenerationContext.Default.APIWorldsRoot
		);
	}

	public static Task<APIStoreItem> GetStoreItem(int id)
	{
		return _client.GetFromJsonAsync(
			ApiUrl("/v1/store/" + id),
			APIGenerationContext.Default.APIStoreItem
		);
	}

#if CREATOR
	public static Task<APILibraryResponse> GetLibrary(LibraryQueryTypeEnum type, int page = 1, string searchQuery = "")
	{
		string queryType = type switch
		{
			LibraryQueryTypeEnum.Model => "model",
			LibraryQueryTypeEnum.Image => "decal",
			LibraryQueryTypeEnum.Audio => "audio",
			LibraryQueryTypeEnum.Mesh => "mesh",
			LibraryQueryTypeEnum.Addon => "addon",
			_ => ""
		};
		return _client.GetFromJsonAsync(
			MainUrl($"/api/library?page={page}&search={searchQuery}&type={queryType}"),
			APIGenerationContext.Default.APILibraryResponse
		);
	}
#endif

	public static Task<string> GetProfanityList()
	{
		return _client.GetStringAsync(ApiUrl("/v1/game/server/profanity"));
	}

	public static Task<MobileAvatarCatalog> GetAvatarCatalog()
	{
		return _client.GetFromJsonAsync(
			ApiUrl("/v1/mobile/avatar/catalog"),
			MobileAvatarGenerationContext.Default.MobileAvatarCatalog
		);
	}

	public static async Task SetAvatar(MobileAvatarSetRequest req)
	{
		HttpResponseMessage response = await _client.PostAsJsonAsync(
			ApiUrl("/v1/mobile/avatar"),
			req,
			MobileAvatarGenerationContext.Default.MobileAvatarSetRequest
		);
		response.EnsureSuccessStatusCode();
	}

	public static Task<MobileProfileResponse> GetProfile(int userID)
	{
		return _client.GetFromJsonAsync(
			ApiUrl("/v1/mobile/profile/" + userID.ToString()),
			MobileAvatarGenerationContext.Default.MobileProfileResponse
		);
	}
}

[JsonSerializable(typeof(MobileAvatarCatalog))]
[JsonSerializable(typeof(MobileAvatarSetRequest))]
[JsonSerializable(typeof(MobileProfileResponse))]
internal partial class MobileAvatarGenerationContext : JsonSerializerContext { }

public struct MobileAvatarItem
{
	[JsonPropertyName("id")] public string Id { get; set; }
	[JsonPropertyName("name")] public string Name { get; set; }
	[JsonPropertyName("thumbnail")] public string Thumbnail { get; set; }
}

public struct MobileAvatarCatalog
{
	[JsonPropertyName("bodies")] public MobileAvatarItem[] Bodies { get; set; }
	[JsonPropertyName("faces")] public MobileAvatarItem[] Faces { get; set; }
	[JsonPropertyName("shirts")] public MobileAvatarItem[] Shirts { get; set; }
	[JsonPropertyName("accessories")] public MobileAvatarItem[] Accessories { get; set; }
}

public struct MobileAvatarSetRequest
{
	[JsonPropertyName("AvatarId")] public string? AvatarId { get; set; }
	[JsonPropertyName("FaceId")] public string? FaceId { get; set; }
	[JsonPropertyName("ShirtId")] public string? ShirtId { get; set; }
	[JsonPropertyName("AccessoryId")] public string? AccessoryId { get; set; }
}

public struct MobileProfileResponse
{
	[JsonPropertyName("success")] public bool Success { get; set; }
	[JsonPropertyName("id")] public int Id { get; set; }
	[JsonPropertyName("username")] public string Username { get; set; }
	[JsonPropertyName("displayName")] public string DisplayName { get; set; }
	[JsonPropertyName("isVerified")] public bool IsVerified { get; set; }
	[JsonPropertyName("bio")] public string Bio { get; set; }
	[JsonPropertyName("isOnline")] public bool IsOnline { get; set; }
	[JsonPropertyName("gameId")] public int GameId { get; set; }
	[JsonPropertyName("gameName")] public string GameName { get; set; }
}
