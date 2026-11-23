using System;
using System.Collections.Generic;
using IrisMenus;
using IrisMenus.UI;
using RimTalk_ToddlersExpansion.Core;
using UnityEngine;
using Verse;

namespace RimTalk_ToddlersExpansion.Integration.Iris
{
	internal sealed class IrisSettingsPage(UiContext context, Action<SettingsListing> draw) : MenuNativePage
	{
		private readonly IrisSettingsListing listing = new IrisSettingsListing(context, draw);

		public override void Draw(Rect rect) => listing.Draw(rect);
		public override void DrawOverlay(Rect viewport) => listing.DrawOverlay(viewport);
		public override void Dispose() => listing.Dispose();
	}

	internal sealed class IrisSettingsListing : SettingsListing, IDisposable
	{
		private readonly UiContext context;
		private readonly MenuScrollView scroll;
		private readonly Action<Listing_Standard> drawListing;
		private readonly Dictionary<string, UiSwitch> switches = new Dictionary<string, UiSwitch>();
		private readonly Dictionary<string, UiSlider> sliders = new Dictionary<string, UiSlider>();
		private readonly Dictionary<string, UiHoverCard> tooltips = new Dictionary<string, UiHoverCard>();
		private readonly Dictionary<string, UiSegments> choices = new Dictionary<string, UiSegments>();
		private Listing_Standard listing;

		internal IrisSettingsListing(UiContext context, Action<IrisSettingsListing> draw)
		{
			this.context = context;
			scroll = new MenuScrollView(context);
			drawListing = current =>
			{
				listing = current;
				draw(this);
			};
		}

		internal void Draw(Rect rect) => scroll.Draw(rect, drawListing);

		public override void Heading(string title)
		{
			DrawLabel(title, context.Palette.Ink, 28f);
			GapLine();
		}

		public override void Label(string text) => DrawLabel(text, context.Palette.Ink);
		public override void Warning(string text) => DrawLabel(text, context.Palette.Error);
		public override void Gap() => listing.Gap(8f * context.Scale);
		public override void GapLine()
		{
			Gap();
			context.Fill(listing.GetRect(1f), context.Palette.Line);
			Gap();
		}

		private void DrawLabel(string text, Color color, float minHeight = 22f)
		{
			float height = MeasureLabel(text, listing.ColumnWidth);
			Rect row = listing.GetRect(Mathf.Max(minHeight * context.Scale, height));
			context.Label(row, text, color, wrap: true);
		}

		private float MeasureLabel(string text, float width)
		{
			float height = UnityStubs.Gui.GuiBackend.Current?.MeasureText(1, context.FontSize, context.FontStyle, text, width).y
				?? Text.CalcHeight(text, width);
			return Mathf.Ceil(height) + 4f * context.Scale;
		}

		public override void CheckboxLabeled(string id, string label, ref bool value, string tooltip = null)
		{
			float scale = context.Scale;
			float labelWidth = Mathf.Max(1f, listing.ColumnWidth - 64f * scale);
			Rect row = listing.GetRect(Mathf.Max(36f * scale, MeasureLabel(label, labelWidth)));
			if (!switches.TryGetValue(id, out UiSwitch control))
			{
				control = new UiSwitch(context);
				switches.Add(id, control);
			}
			context.Label(new Rect(row.x, row.y, labelWidth, row.height), label, context.Palette.Ink, wrap: true);
			value = control.Draw(row, value, scale: scale);

			if (!string.IsNullOrEmpty(tooltip))
			{
				if (!tooltips.TryGetValue(id, out UiHoverCard card))
				{
					card = new UiHoverCard(context);
					tooltips.Add(id, card);
				}
				card.Observe(row, tooltip, control.Id, context.IsRectVisible(row));
			}
		}

		public override void PawnIdentity(Pawn pawn, string label)
		{
			float portraitSize = 52f * context.Scale;
			float labelOffset = portraitSize + 8f * context.Scale;
			float labelWidth = Mathf.Max(1f, listing.ColumnWidth - labelOffset);
			Rect row = listing.GetRect(Mathf.Max(portraitSize, MeasureLabel(label, labelWidth)));
			if (context.IsRectVisible(row))
			{
				DrawPawnPortrait(new Rect(row.x, row.y, portraitSize, portraitSize), pawn);
			}
			context.Label(new Rect(row.x + labelOffset, row.y, labelWidth, row.height), label, context.Palette.Ink, wrap: true);
		}

		public override int Choice(string id, string[] options, int selected)
		{
			if (!choices.TryGetValue(id, out UiSegments control))
			{
				control = new UiSegments(context, options);
				choices.Add(id, control);
			}
			return control.Draw(ControlRow(), selected, scale: context.Scale);
		}

		protected override float DrawSlider(string key, string label, float value, float min, float max, float step)
		{
			Label(label);
			if (!sliders.TryGetValue(key, out UiSlider control))
			{
				control = new UiSlider(context, min, max, step);
				sliders.Add(key, control);
			}
			return control.Draw(ControlRow(), value, scale: context.Scale);
		}

		internal Rect ControlRow() => listing.GetRect(36f * context.Scale);

		internal string TextField(UiTextField field, string label, string value)
		{
			Label(label);
			string result = field.Draw(ControlRow(), value ?? string.Empty, scale: context.Scale);
			Gap();
			return result;
		}

		internal void DrawOverlay(Rect viewport)
		{
			foreach (UiHoverCard card in tooltips.Values)
			{
				card.DrawOverlay(viewport, context.Scale);
			}
		}

		public void Dispose()
		{
			foreach (UiSegments control in choices.Values)
			{
				for (int i = 0; i < control.Count; i++)
				{
					Release(control.GetItemId(i));
				}
			}
			foreach (UiSwitch control in switches.Values)
			{
				Release(control.Id);
			}
			foreach (UiSlider control in sliders.Values)
			{
				Release(control.Id);
			}
			foreach (UiHoverCard card in tooltips.Values)
			{
				card.Dismiss();
			}
			scroll.Dispose();
		}

		internal void Release(UiId id)
		{
			context.Release(id);
			if (context.IsFocused(id))
			{
				context.Focus(default);
			}
		}
	}
}
