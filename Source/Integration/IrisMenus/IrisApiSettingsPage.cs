using System;
using System.Linq;
using IrisMenus;
using IrisMenus.UI;
using RimTalk_ToddlersExpansion.Core;
using RimTalk_ToddlersExpansion.Integration.RimTalk;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Iris
{
	internal sealed class IrisApiSettingsPage : MenuNativePage
	{
		private const string KeyPrefix = "RimTalk_ToddlersExpansion_Llm_";
		private static readonly ToddlersExpansionStandaloneLlmProvider[] Providers = Enum.GetValues<ToddlersExpansionStandaloneLlmProvider>();
		private static readonly string[] ProviderLabels = Providers.Select(provider => provider.GetLabel()).ToArray();
		private readonly UiContext context;
		private readonly ToddlersExpansionSettings settings;
		private readonly IrisSettingsListing listing;
		private readonly UiSegments modes;
		private readonly UiSelect providerMenu;
		private readonly UiTextField baseUrl, apiKey, model;

		internal IrisApiSettingsPage(UiContext context, ToddlersExpansionSettings settings)
		{
			this.context = context;
			this.settings = settings;
			listing = new IrisSettingsListing(context, DrawContent);
			modes = new UiSegments(context, ModeLabels());
			providerMenu = new UiSelect(context, ProviderLabels);
			baseUrl = new UiTextField(context);
			apiKey = new UiTextField(context);
			model = new UiTextField(context);
		}

		public override void Prepare()
		{
			if (settings.UseStandaloneLlmApi)
			{
				providerMenu.Prepare();
			}
		}

		public override void Draw(Rect rect) => listing.Draw(rect);

		private void DrawContent(IrisSettingsListing list)
		{
			list.Heading((KeyPrefix + "Settings_Header").Translate());
			list.Label((KeyPrefix + "Settings_Desc").Translate());
			list.Gap();

			bool standalone = settings.UseStandaloneLlmApi;
			settings.UseStandaloneLlmApi = modes.Draw(list.ControlRow(), standalone ? 1 : 0, scale: context.Scale) == 1;
			if (standalone && !settings.UseStandaloneLlmApi)
			{
				ReleaseApiFields();
			}
			list.Gap();
			list.Label((KeyPrefix + "ScopeNote").Translate());
			list.Gap();

			if (settings.UseStandaloneLlmApi)
			{
				DrawStandaloneConfig(list, settings.StandaloneApi);
			}
			if (!RimTalkCompatUtility.IsRimTalkActive)
			{
				list.Label((KeyPrefix + "RimTalkUnavailable").Translate());
			}
		}

		private void DrawStandaloneConfig(IrisSettingsListing list, ToddlersExpansionStandaloneApiConfig config)
		{
			list.Label((KeyPrefix + "Provider").Translate());
			providerMenu.Draw(list.ControlRow(), Array.IndexOf(Providers, config.Provider), scale: context.Scale);
			list.Gap();

			if (config.RequiresCustomBaseUrl)
			{
				config.BaseUrl = list.TextField(baseUrl, (KeyPrefix + "BaseUrl").Translate(), config.BaseUrl);
			}
			else
			{
				list.Label((KeyPrefix + "Endpoint").Translate(config.GetResolvedBaseUrl()));
				list.Gap();
			}
			config.ApiKey = list.TextField(apiKey, (KeyPrefix + "ApiKey").Translate(), config.ApiKey);
			config.Model = list.TextField(model, (KeyPrefix + "Model").Translate(), config.Model);
			if (!config.IsValid())
			{
				list.Warning((KeyPrefix + "ConfigWarning").Translate());
			}
		}

		public override void DrawOverlay(Rect viewport)
		{
			if (!settings.UseStandaloneLlmApi)
			{
				return;
			}

			ToddlersExpansionStandaloneApiConfig config = settings.StandaloneApi;
			int previous = Array.IndexOf(Providers, config.Provider);
			int next = providerMenu.DrawPopup(viewport, previous);
			if (next != previous)
			{
				config.SelectProvider(Providers[next]);
				if (!config.RequiresCustomBaseUrl)
				{
					baseUrl.Dispose();
				}
			}
		}

		private static string[] ModeLabels() =>
		[
			(KeyPrefix + (RimTalkCompatUtility.IsRimTalkActive ? "UseRimTalk" : "Disabled")).Translate(),
			(KeyPrefix + "UseStandalone").Translate()
		];

		private void ReleaseApiFields()
		{
			providerMenu.Close();
			listing.Release(providerMenu.Id);
			baseUrl.Dispose();
			apiKey.Dispose();
			model.Dispose();
		}

		public override void Dispose()
		{
			ReleaseApiFields();
			listing.Release(modes.GetItemId(0));
			listing.Release(modes.GetItemId(1));
			listing.Dispose();
		}
	}
}
