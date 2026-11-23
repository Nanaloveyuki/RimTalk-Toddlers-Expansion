using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	internal abstract class SettingsListing
	{
		public abstract void Heading(string title);
		public abstract void Label(string text);
		public abstract void Warning(string text);
		public abstract void Gap();
		public abstract void GapLine();
		public abstract void CheckboxLabeled(string key, ref bool value, string tooltip = null);

		public int Slider(string key, int value, int min, int max, params NamedArgument[] labelArguments) =>
			(int)DrawSlider(key, key.Translate(labelArguments), value, min, max, 1f);

		public float Slider(string key, float value, float min, float max, params NamedArgument[] labelArguments) =>
			DrawSlider(key, key.Translate(labelArguments), value, min, max, 0f);

		protected abstract float DrawSlider(string key, string label, float value, float min, float max, float step);
	}

	internal sealed class VanillaSettingsListing(Listing_Standard listing) : SettingsListing
	{
		public override void Heading(string title)
		{
			listing.Label(title);
			listing.GapLine();
		}

		public override void Label(string text) => listing.Label(text);

		public override void Warning(string text)
		{
			GUI.color = Color.yellow;
			listing.Label(text);
			GUI.color = Color.white;
		}

		public override void Gap() => listing.Gap();
		public override void GapLine() => listing.GapLine();

		public override void CheckboxLabeled(string key, ref bool value, string tooltip = null) =>
			listing.CheckboxLabeled(key.Translate(), ref value, tooltip);

		protected override float DrawSlider(string key, string label, float value, float min, float max, float step)
		{
			listing.Label(label);
			return listing.Slider(value, min, max);
		}
	}
}
