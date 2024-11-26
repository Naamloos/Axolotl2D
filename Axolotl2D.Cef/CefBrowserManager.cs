using CefSharp.OffScreen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Axolotl2D.Cef
{
    public class CefBrowserManager
    {
        private readonly Dictionary<string, CefBrowser> _browsers = new();
        private readonly ILazyDependencyLoader<Game> _lazyGame;

        public CefBrowserManager(ILazyDependencyLoader<Game> game)
        {
            _lazyGame = game;

            var cefSett = new CefSettings
            {
                RootCachePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? string.Empty) ?? string.Empty, "cefCache"),
                WindowlessRenderingEnabled = true,
                LogSeverity = CefSharp.LogSeverity.Verbose,
                LogFile = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location ?? string.Empty) ?? string.Empty, "cef.log")
            };
            CefSharp.Cef.Initialize(cefSett, true, browserProcessHandler: null);
        }

        public void RegisterBrowser(string key, string baseUrl)
        {
            if (!_lazyGame.IsLoaded)
            {
                throw new Exception("Attempted to load assets before game initialization!");
            }
            if (_browsers.ContainsKey(key))
            {
                throw new Exception($"Browser with key {key} already exists!");
            }
            var _game = _lazyGame.Value;
            var browser = new CefBrowser(_game, Vector2.Zero, Vector2.One, baseUrl);
            _browsers.Add(key, browser);
        }

        public bool TryGetBrowser(string key, out CefBrowser? browser)
        {
            var success = _browsers.TryGetValue(key, out CefBrowser? output);
            browser = output;
            return success;
        }
    }
}
