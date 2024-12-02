using Axolotl2D.Helpers;
using CefSharp.OffScreen;
using System.Numerics;
using System.Reflection;

namespace Axolotl2D.Cef
{
    /// <summary>
    /// Manages CEF browsers.
    /// </summary>
    public class CefBrowserManager
    {
        private readonly Dictionary<string, CefBrowser> registeredBrowsers = new();
        private readonly ILazyDependencyLoader<Game> lazyLoadedGame;

        /// <summary>
        /// Creates a new instance of <see cref="CefBrowserManager"/>.
        /// </summary>
        /// <param name="game">Game to use for initialization</param>
        public CefBrowserManager(ILazyDependencyLoader<Game> game)
        {
            lazyLoadedGame = game;

            var cefSett = new CefSettings
            {
                RootCachePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? string.Empty) ?? string.Empty, "cefCache"),
                WindowlessRenderingEnabled = true,
                LogSeverity = CefSharp.LogSeverity.Verbose,
                LogFile = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? string.Empty) ?? string.Empty, "cef.log")
            };
            CefSharp.Cef.Initialize(cefSett, true, browserProcessHandler: null);
        }

        /// <summary>
        /// Registers a browser with the given key and base URL.
        /// </summary>
        /// <param name="key">Key to register browser under</param>
        /// <param name="baseUrl">URL to register browser with</param>
        public void RegisterBrowser(string key, string baseUrl)
        {
            if (!lazyLoadedGame.IsLoaded)
            {
                throw new Exception("Attempted to load assets before game initialization!");
            }
            if (registeredBrowsers.ContainsKey(key))
            {
                throw new Exception($"Browser with key {key} already exists!");
            }
            var _game = lazyLoadedGame.Value;
            var browser = new CefBrowser(_game, Vector2.Zero, Vector2.One, baseUrl);
            registeredBrowsers.Add(key, browser);
        }

        /// <summary>
        /// Tries to get a browser from the asset manager.
        /// </summary>
        /// <param name="key">Key of browser to get</param>
        /// <param name="browser">a <see cref="CefBrowser"/></param>
        /// <returns>Whether retrieving was succesful.</returns>
        public bool TryGetBrowser(string key, out CefBrowser? browser)
        {
            var success = registeredBrowsers.TryGetValue(key, out CefBrowser? output);
            browser = output;
            return success;
        }
    }
}
