using RimWorld;
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
		public void CheckboxLabeled(string key, ref bool value, string tooltip = null) =>
			CheckboxLabeled(key, key.Translate(), ref value, tooltip);

		public abstract void CheckboxLabeled(string id, string label, ref bool value, string tooltip = null);
		public abstract void PawnIdentity(Pawn pawn, string label);
		public abstract int Choice(string id, string[] options, int selected);

		protected static void DrawPawnPortrait(Rect rect, Pawn pawn)
		{
			if (Event.current.type == EventType.Repaint)
			{
				Texture portrait = PortraitsCache.Get(pawn, rect.size, Rot4.South, renderHeadgear: false);
				GUI.DrawTexture(rect, portrait, ScaleMode.ScaleToFit, true);
			}
		}

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

		public override void CheckboxLabeled(string id, string label, ref bool value, string tooltip = null) =>
			listing.CheckboxLabeled(label, ref value, tooltip);

		public override void PawnIdentity(Pawn pawn, string label)
		{
			const float portraitSize = 52f;
			const float labelOffset = 60f;
			float labelWidth = Mathf.Max(1f, listing.ColumnWidth - labelOffset);
			Rect row = listing.GetRect(Mathf.Max(portraitSize, Text.CalcHeight(label, labelWidth)));
			DrawPawnPortrait(new Rect(row.x, row.y, portraitSize, portraitSize), pawn);
			Widgets.Label(new Rect(row.x + labelOffset, row.y, labelWidth, row.height), label);
		}

		public override int Choice(string id, string[] options, int selected)
		{
			Rect row = listing.GetRect(Text.LineHeight);
			float width = row.width / options.Length;
			for (int i = 0; i < options.Length; i++)
			{
				if (Widgets.RadioButtonLabeled(new Rect(row.x + width * i, row.y, width, row.height), options[i], selected == i))
				{
					return i;
				}
			}
			return selected;
		}

		protected override float DrawSlider(string key, string label, float value, float min, float max, float step)
		{
			listing.Label(label);
			return listing.Slider(value, min, max);
		}
	}
}
