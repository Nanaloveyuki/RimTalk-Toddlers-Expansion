using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimTalk_ToddlersExpansion.Integration.Toddlers;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimTalk_ToddlersExpansion.Integration.RimTalk
{
	public enum ToddlerPlayReportKind
	{
		SelfPlay,
		MutualPlay
	}

	public static class ToddlerPlayReportUtility
	{
		private const int RequestCooldownTicks = 12000;
		private const int ContextMaxChars = 180;

		private const string OutputRules = "Output only the activity phrase, 1-4 words, no punctuation, no quotes.";
		private const string SafetyRules = "The activity must be safe, gentle, warm, playful, and age-appropriate for a toddler. Treat buildings, utilities, adult objects, and technical objects as scenery unless they are explicitly toddler toys. Do not lazily convert a nearby object into 'play with that object'. If the scene contains adult or technical objects, use passive observation, imagination, colors, sounds, shadows, shapes, or nearby ground play instead. Prefer ordinary toddler behavior such as crawling, wobbling, giggling, babbling, peekaboo, soft toys, blocks, leaves, shadows, snow, harmless floor play, or playing beside another toddler.";

		// Loading a save creates a new Game, which can reuse pawn and job IDs.
		private static readonly ConditionalWeakTable<Game, ReportSession> Sessions = new ConditionalWeakTable<Game, ReportSession>();

		public static void EnsureReportRequested(Job job, Pawn toddler, Pawn partner, ToddlerPlayReportKind kind)
		{
			if (job == null || toddler == null || !RimTalkCompatUtility.CanRequestShortText)
			{
				return;
			}

			if (!string.IsNullOrWhiteSpace(job.reportStringOverride))
			{
				return;
			}

			int jobId = job.loadID;
			Game game = Current.Game;
			if (game == null || jobId < 0)
			{
				return;
			}

			ReportSession session = Sessions.GetValue(game, static _ => new ReportSession());
			ReportRequest request = session.TryRegister(jobId, toddler.thingIDNumber, Find.TickManager.TicksGame);
			if (request == null)
			{
				return;
			}

			string nearby = RimTalkCompatUtility.TryGetNearbyContextText(toddler, out string contextText)
				? contextText
				: null;
			string sceneContext = BuildToddlerPlaySceneContext(toddler, job, TrimContext(nearby));
			string partnerLabel = partner?.LabelShort ?? "RimTalk_ToddlersExpansion_PlayReport_AnotherToddler".Translate();
			string prefix = BuildPrefix(kind, partnerLabel);

			string systemPrompt = BuildSystemPrompt(kind);
			string userPrompt = BuildUserPrompt(kind, sceneContext, partnerLabel);

			bool queued = RimTalkCompatUtility.TryRequestShortText(systemPrompt, userPrompt, response =>
			{
				string sanitized = SanitizeResponse(response);
				string report = string.IsNullOrEmpty(sanitized) ? null : prefix + sanitized;
				session.Complete(jobId, request, report);
			});

			if (!queued)
			{
				session.Complete(jobId, request, null);
			}
		}

		public static void TryApplyPendingReport(Job job)
		{
			if (job == null || job.loadID < 0)
			{
				return;
			}

			Game game = Current.Game;
			if (game != null && Sessions.TryGetValue(game, out ReportSession session)
				&& session.TryTakeReport(job.loadID, out string report))
			{
				job.reportStringOverride = report;
			}
		}

		public static string ActiveLanguageName => LanguageDatabase.activeLanguage?.info?.friendlyNameNative ?? "English";

		public static void CancelJob(Job job)
		{
			Game game = Current.Game;
			if (job != null && job.loadID >= 0 && game != null
				&& Sessions.TryGetValue(game, out ReportSession session))
			{
				session.Cancel(job.loadID);
			}
		}

		private static string BuildPrefix(ToddlerPlayReportKind kind, string partnerLabel)
		{
			return kind == ToddlerPlayReportKind.SelfPlay
				? "RimTalk_ToddlersExpansion_PlayReport_SelfPrefix".Translate()
				: "RimTalk_ToddlersExpansion_PlayReport_MutualPrefix".Translate(partnerLabel.Named("PARTNER"));
		}

		private static string BuildSystemPrompt(ToddlerPlayReportKind kind)
		{
			string activity = kind == ToddlerPlayReportKind.SelfPlay
				? "Return a short toddler self-play activity."
				: "Return a short toddler shared play activity.";
			string lang = ActiveLanguageName;
			string langRule = string.IsNullOrWhiteSpace(lang)
				? "Use the game's language."
				: $"Use the game's language: {lang}.";
			return $"{activity} {langRule} {SafetyRules} {OutputRules}";
		}

		private static string BuildUserPrompt(ToddlerPlayReportKind kind, string sceneContext, string partnerLabel)
		{
			string context = string.IsNullOrWhiteSpace(sceneContext) ? "none" : sceneContext;
			if (kind == ToddlerPlayReportKind.SelfPlay)
			{
				return $"Scene context: {context}. Toddler is self-playing. Suggest a concrete, harmless, ordinary toddler play activity that fits this scene. If nearby objects are not toddler toys, use them only as visual background.";
			}

			return $"Scene context: {context}. Toddler is playing with {partnerLabel}. Suggest a concrete, harmless, ordinary shared toddler play activity that fits this scene. If nearby objects are not toddler toys, use them only as visual background.";
		}

		private static string BuildToddlerPlaySceneContext(Pawn toddler, Job job, string nearby)
		{
			if (toddler?.Map == null)
			{
				return string.IsNullOrWhiteSpace(nearby) ? "none" : $"Surroundings: {nearby}";
			}

			Map map = toddler.Map;
			IntVec3 spot = GetPlaySpot(toddler, job);
			TerrainDef terrain = spot.InBounds(map) ? spot.GetTerrain(map) : null;
			Room room = spot.InBounds(map) ? spot.GetRoom(map) : null;
			bool outdoors = room?.PsychologicallyOutdoors ?? true;
			string roomRole = room?.Role?.label;
			string weather = map.weatherManager?.curWeather?.label;
			string season = GenLocalDate.Season(map).Label();
			string temperature = Mathf.RoundToInt(spot.GetTemperature(map)).ToString();
			string snow = map.snowGrid != null && spot.InBounds(map)
				? map.snowGrid.GetDepth(spot).ToStringPercent()
				: "0%";
			string nearbyPawns = BuildNearbyPawnContext(toddler);

			var parts = new List<string>
			{
				$"Place: {(outdoors ? "outdoors" : "indoors")}",
				!string.IsNullOrWhiteSpace(roomRole) ? $"Room: {roomRole}" : null,
				terrain != null ? $"Ground: {terrain.LabelCap}" : null,
				!string.IsNullOrWhiteSpace(weather) ? $"Weather: {weather}" : null,
				!string.IsNullOrWhiteSpace(season) ? $"Season: {season}" : null,
				$"Temperature: {temperature}C",
				$"Snow: {snow}",
				!string.IsNullOrWhiteSpace(nearbyPawns) ? $"Nearby pawns: {nearbyPawns}" : null,
				!string.IsNullOrWhiteSpace(nearby) ? $"Nearby objects as scenery: {nearby}" : null
			};

			return string.Join("; ", parts.FindAll(part => !string.IsNullOrWhiteSpace(part)));
		}

		private static IntVec3 GetPlaySpot(Pawn toddler, Job job)
		{
			if (job != null && job.targetB.IsValid && job.targetB.Cell.IsValid)
			{
				return job.targetB.Cell;
			}

			if (job != null && job.targetA.IsValid && job.targetA.Cell.IsValid)
			{
				return job.targetA.Cell;
			}

			return toddler.Position;
		}

		private static string BuildNearbyPawnContext(Pawn toddler)
		{
			if (toddler?.Map?.mapPawns == null)
			{
				return null;
			}

			int toddlers = 0;
			int children = 0;
			int adults = 0;
			int animals = 0;

			IReadOnlyList<Pawn> pawns = toddler.Map.mapPawns.AllPawnsSpawned;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn other = pawns[i];
				if (other == null || other == toddler || other.Dead || !other.Spawned)
				{
					continue;
				}

				if (!other.Position.InHorDistOf(toddler.Position, 8f))
				{
					continue;
				}

				if (other.RaceProps?.Animal == true)
				{
					animals++;
				}
				else if (ToddlersCompatUtility.IsToddler(other) || other.DevelopmentalStage.Baby() || other.DevelopmentalStage.Newborn())
				{
					toddlers++;
				}
				else if (other.DevelopmentalStage.Child())
				{
					children++;
				}
				else if (other.RaceProps?.Humanlike == true)
				{
					adults++;
				}
			}

			var parts = new List<string>();
			if (toddlers > 0)
			{
				parts.Add($"{toddlers} toddler/baby");
			}
			if (children > 0)
			{
				parts.Add($"{children} child");
			}
			if (adults > 0)
			{
				parts.Add($"{adults} adult");
			}
			if (animals > 0)
			{
				parts.Add($"{animals} animal");
			}

			return parts.Count == 0 ? null : string.Join(", ", parts);
		}

		private static string TrimContext(string context)
		{
			if (string.IsNullOrWhiteSpace(context))
			{
				return string.Empty;
			}

			string normalized = context.Replace('\r', ' ').Replace('\n', ';').Trim();
			if (normalized.Length > ContextMaxChars)
			{
				normalized = normalized.Substring(0, ContextMaxChars).Trim();
			}

			return normalized;
		}

		private static string SanitizeResponse(string response)
		{
			if (string.IsNullOrWhiteSpace(response))
			{
				return string.Empty;
			}

			string line = response.Trim();
			int breakAt = line.IndexOfAny(new[] { '\r', '\n' });
			if (breakAt >= 0)
			{
				line = line.Substring(0, breakAt);
			}

			line = line.Trim().Trim('"', '\'', '`');
			if (line.StartsWith("-", StringComparison.Ordinal))
			{
				line = line.TrimStart('-', ' ', '\t');
			}

			line = line.TrimEnd('.', '!', '?', ';', ':').Trim();
			if (line.Length > 60)
			{
				line = line.Substring(0, 60).Trim();
			}
			return line;
		}

		private sealed class ReportSession
		{
			private readonly object _sync = new object();
			private readonly Dictionary<int, ReportRequest> _requests = new Dictionary<int, ReportRequest>(64);
			private readonly Dictionary<int, int> _lastRequestTicks = new Dictionary<int, int>(64);

			public ReportRequest TryRegister(int jobId, int pawnId, int now)
			{
				lock (_sync)
				{
					if (_requests.ContainsKey(jobId)
						|| (_lastRequestTicks.TryGetValue(pawnId, out int lastTick) && now - lastTick < RequestCooldownTicks))
					{
						return null;
					}

					ReportRequest request = new ReportRequest();
					_requests.Add(jobId, request);
					_lastRequestTicks[pawnId] = now;
					return request;
				}
			}

			public void Complete(int jobId, ReportRequest request, string report)
			{
				lock (_sync)
				{
					// A callback can arrive after the same job canceled and queued a new request.
					if (!_requests.TryGetValue(jobId, out ReportRequest pending) || pending != request || pending.Report != null)
					{
						return;
					}

					if (report == null)
					{
						_requests.Remove(jobId);
					}
					else
					{
						pending.Report = report;
					}
				}
			}

			public bool TryTakeReport(int jobId, out string report)
			{
				lock (_sync)
				{
					report = _requests.TryGetValue(jobId, out ReportRequest request) ? request.Report : null;
					return report != null && _requests.Remove(jobId);
				}
			}

			public void Cancel(int jobId)
			{
				lock (_sync)
				{
					_requests.Remove(jobId);
				}
			}
		}

		private sealed class ReportRequest
		{
			public string Report;
		}
	}
}
