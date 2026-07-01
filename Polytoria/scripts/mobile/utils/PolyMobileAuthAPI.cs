// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using Godot;
using Polytoria.Schemas.API;
using Polytoria.Shared;
using Polytoria.Utils;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Polytoria.Mobile.Utils;

public static class PolyMobileAuthAPI
{
	private static readonly PTHttpClient _client = new();

	public static event Action<APIMeResponse>? UserAuthenticated;
	public static event Action? AskForAuthentication;

	public static APIMeResponse CurrentUserInfo { get; private set; }
	public static string CurrentToken => _authData.Token ?? "";

	private static MobileAuthData _authData;
	private const string AuthDataPath = "user://auth2";

	public static async void SetupClient()
	{
		_authData = new();

		if (FileAccess.FileExists(AuthDataPath))
		{
			using FileAccess access = FileAccess.Open(AuthDataPath, FileAccess.ModeFlags.Read);
			string data = access.GetAsText();
			access.Close();
			MobileAuthData? auth = JsonSerializer.Deserialize(data, MobileAuthDataGenerationContext.Default.MobileAuthData);
			if (auth != null)
			{
				PT.Print("Existing auth data exists, using");
				PT.Print(_authData.Token);
				_authData = auth.Value;
			}
		}

		if (_authData.Token == null)
		{
			AskForAuthentication?.Invoke();
		}
		else
		{
			await LoginWithAuthToken(_authData.Token!);
		}
	}

	private static void SaveAuthData()
	{
		FileAccess authData = FileAccess.Open(AuthDataPath, FileAccess.ModeFlags.Write);
		authData.StoreString(JsonSerializer.Serialize(_authData, MobileAuthDataGenerationContext.Default.MobileAuthData));
		authData.Close();
	}

	private static void ClearAuthData()
	{
		_authData = new();
		if (FileAccess.FileExists(AuthDataPath))
		{
			DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(AuthDataPath));
		}
	}

	public static async Task LoginWithCodeAndState(string code, string state)
	{
		using HttpResponseMessage response = await _client.PostAsJsonAsync(BuildUrl(Globals.ApiEndpoint, "v1/mobile/token"), new APIMobileTokenRequest()
		{
			Code = code,
			State = state
		}, APIGenerationContext.Default.APIMobileTokenRequest);
		if (response.IsSuccessStatusCode)
		{
			APIMobileTokenResponse tokenRes = await response.Content.ReadFromJsonAsync(APIGenerationContext.Default.APIMobileTokenResponse);
			if (tokenRes.Success)
			{
				await LoginWithAuthToken(tokenRes.Token);
			}
		}
		else
		{
			PT.PrintErr(response);
			throw new AuthenticationException("Something went wrong");
		}
	}

	public static async Task<string?> LoginWithCredentials(string username, string password, bool signup)
	{
		string path = signup ? "v1/mobile/signup" : "v1/mobile/login";
		using HttpResponseMessage response = await _client.PostAsJsonAsync(BuildUrl(Globals.ApiEndpoint, path), new MobileCredentialsRequest()
		{
			Username = username,
			Password = password
		}, MobileCredentialsGenerationContext.Default.MobileCredentialsRequest);

		MobileCredentialsResponse result = await response.Content.ReadFromJsonAsync(MobileCredentialsGenerationContext.Default.MobileCredentialsResponse);
		if (!result.Success || string.IsNullOrWhiteSpace(result.Token))
		{
			return string.IsNullOrWhiteSpace(result.Error) ? "Login fehlgeschlagen." : result.Error;
		}

		await LoginWithAuthToken(result.Token!);
		return null;
	}

	public static async Task LoginWithAuthToken(string userToken)
	{
		PolyAPI.SetAuthToken(userToken);
		try
		{
			APIMeResponse me = await PolyAPI.GetCurrentUser();

			_authData.Username = me.Username;
			_authData.Token = userToken;
			_authData.UserID = me.Id;
			SaveAuthData();
			PT.Print("Hello!! ", me.Username);

			CurrentUserInfo = me;
			UserAuthenticated?.Invoke(me);
		}
		catch (Exception ex)
		{
			PT.PrintErr(ex);
			ClearAuthData();
			AskForAuthentication?.Invoke();
		}
	}

	private static string BuildUrl(string endpoint, string path)
	{
		return endpoint.TrimEnd('/') + "/" + path.TrimStart('/');
	}
}


[JsonSerializable(typeof(MobileAuthData))]
internal partial class MobileAuthDataGenerationContext : JsonSerializerContext { }

[JsonSerializable(typeof(MobileCredentialsRequest))]
[JsonSerializable(typeof(MobileCredentialsResponse))]
internal partial class MobileCredentialsGenerationContext : JsonSerializerContext { }

public struct MobileCredentialsRequest
{
	[JsonPropertyName("username")]
	public string Username { get; set; }

	[JsonPropertyName("password")]
	public string Password { get; set; }
}

public struct MobileCredentialsResponse
{
	[JsonPropertyName("success")]
	public bool Success { get; set; }

	[JsonPropertyName("token")]
	public string? Token { get; set; }

	[JsonPropertyName("error")]
	public string? Error { get; set; }
}

public struct MobileAuthData
{
	[JsonInclude]
	public string Token { get; set; }

	[JsonInclude]
	public int UserID { get; set; }

	[JsonInclude]
	public string Username { get; set; }
}
