using System.Reflection;
using System.Xml;
using HarmonyLib;
using Verse;

namespace RimTalk_ToddlersExpansion.Harmony
{
	internal static class Patch_LegacyGameComponents
	{
		private const string RemovedAgeComponent = "RimTalk_ToddlersExpansion.Integration.Toddlers.ToddlerAgeSettingsGameComponent";

		internal static void Init(HarmonyLib.Harmony harmony)
		{
			MethodInfo exposeComponents = AccessTools.DeclaredMethod(typeof(Game), "ExposeSmallComponents");
			if (exposeComponents == null)
			{
				Log.Warning("[RimTalk_ToddlersExpansion] Could not find Game.ExposeSmallComponents for legacy save migration.");
				return;
			}

			harmony.Patch(exposeComponents, prefix: new HarmonyMethod(typeof(Patch_LegacyGameComponents), nameof(RemoveLegacyComponents)));
		}

		private static void RemoveLegacyComponents()
		{
			if (Scribe.mode != LoadSaveMode.LoadingVars)
			{
				return;
			}

			// Remove the stateless component before Scribe tries to resolve its class.
			XmlElement components = Scribe.loader.curXmlParent?["components"];
			for (XmlNode node = components?.LastChild; node != null;)
			{
				XmlNode previous = node.PreviousSibling;
				if (node is XmlElement element && element.Name == "li" && element.GetAttribute("Class") == RemovedAgeComponent)
				{
					components.RemoveChild(element);
				}
				node = previous;
			}
		}
	}
}
