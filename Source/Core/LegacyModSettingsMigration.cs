using System;
using System.IO;
using Verse;

namespace RimTalk_ToddlersExpansion.Core
{
	internal static class LegacyModSettingsMigration
	{
		private const string OriginalPackageId = "cj.rimtalk.toddlers";
		private const string OriginalWorkshopFolder = "3659064387";

		internal static void CopyIfMissing(string destinationFolder, string modClassName)
		{
			string target = GetSettingsPath(destinationFolder, modClassName);
			if (File.Exists(target))
			{
				return;
			}

			string originalFolder = ModLister.GetModWithIdentifier(OriginalPackageId)?.FolderName ?? OriginalWorkshopFolder;
			string source = GetSettingsPath(originalFolder, modClassName);
			if (!File.Exists(source))
			{
				source = GetSettingsPath(OriginalWorkshopFolder, modClassName);
				if (!File.Exists(source))
				{
					return;
				}
			}

			string pending = target + "." + Path.GetRandomFileName();
			try
			{
				try
				{
					File.Copy(source, pending, overwrite: false);
					File.Move(pending, target);
				}
				finally
				{
					File.Delete(pending);
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				if (!File.Exists(target))
				{
					Log.Warning("[RimTalk_ToddlersExpansion] Could not copy the original mod settings to the Continue profile.");
				}
			}
		}

		private static string GetSettingsPath(string folder, string modClassName)
		{
			return Path.Combine(GenFilePaths.ConfigFolderPath, GenText.SanitizeFilename($"Mod_{folder}_{modClassName}.xml"));
		}
	}
}
