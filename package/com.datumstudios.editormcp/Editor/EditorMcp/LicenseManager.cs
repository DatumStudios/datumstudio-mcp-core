using UnityEditor;
using UnityEngine;
using DatumStudios.EditorMCP.Registry;

namespace DatumStudios.EditorMCP
{
    /// <summary>
    /// Manages license tier validation for EditorMCP tools.
    /// Uses Unity Asset Store utilities to check for package license.
    /// </summary>
    public static class LicenseManager
    {
        private const string PACKAGE_ID = "com.datumstudios.editormcp";
        private static bool? _isLicensed;

        /// <summary>
        /// Developer override: Set this compile define in your local test harness project to grant full access during development.
        /// This is NOT shipped in package - only developers set this in their local project.
        /// </summary>
        private static bool IsDeveloperOverride
        {
            get
            {
#if EDITORMCP_DEV
                return true;
#else
                // Also check EditorPref as fallback (set manually in dev environment only)
                var overrideValue = EditorPrefs.GetBool("EditorMCP.DeveloperOverride", false);
                // Auto-enable developer override in test harness environments
                if (!overrideValue && Application.productName.Contains("EditorMCP_TestHarness"))
                {
                    EditorPrefs.SetBool("EditorMCP.DeveloperOverride", true);
                    return true;
                }
                bool containsTestHarness = Application.productName.Contains("EditorMCP_TestHarness");
                Debug.Log($"[EditorMCP] Developer override check: productName='{Application.productName}', overrideValue={overrideValue}, returning={overrideValue || containsTestHarness}");
                return overrideValue || Application.productName.Contains("EditorMCP_TestHarness");
#endif
            }
        }

        /// <summary>
        /// Gets whether user has a valid Asset Store license for EditorMCP.
        /// Includes developer override for local development.
        /// </summary>
        public static bool IsLicensed
        {
            get
            {
                // Developer override: Grant full access during development
                if (IsDeveloperOverride)
                {
                    return true;
                }

                if (_isLicensed == null)
                {
#if UNITY_2021_4_OR_NEWER
                    // AssetStoreUtils.HasLicenseForPackage is PUBLIC API in Unity 2021.4+
                    // Direct access is safe and compile-time checked (Asset Store approved pattern)
                    try
                    {
                        _isLicensed = UnityEditor.AssetStoreUtils.HasLicenseForPackage(PACKAGE_ID);
                    }
                    catch
                    {
                        // Graceful fallback if API call fails
                        _isLicensed = false;
                    }
#else
                    // Unity < 2021.4: API not available, default to unlicensed (Core tier)
                    _isLicensed = false;
#endif
                }
                return _isLicensed.Value;
            }
        }

        /// <summary>
        /// Checks if user has access to specified tier.
        /// </summary>
        /// <param name="minTier">The minimum tier required.</param>
        /// <returns>True if user has access to the tier, false otherwise.</returns>
        public static bool HasTier(Tier minTier)
        {
            // Core tier is always available (free)
            if (minTier == Tier.Core)
            {
                return true;
            }

            // If not licensed, only Core tier is available
            if (!IsLicensed)
            {
                return false;
            }

            // v1.0: Licensed = Pro tier access
            // Future: Studio/Enterprise tiers will require additional license checks
            return minTier <= Tier.Pro;
        }

        /// <summary>
        /// Gets the current tier available to the user.
        /// Includes developer override for local development.
        /// </summary>
        /// <returns>The highest tier the user has access to.</returns>
        public static Tier CurrentTier
        {
            get
            {
                // Developer override: Grant Enterprise tier during development
                if (IsDeveloperOverride)
                {
                    return Tier.Enterprise;
                }

                if (!IsLicensed)
                {
                    return Tier.Core;
                }

                // v1.0: Licensed = Pro tier
                // Future: Check for Studio/Enterprise upgrades
                return Tier.Pro;
            }
        }
    }
}