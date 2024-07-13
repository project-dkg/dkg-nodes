using dkgNode.Models;
using Microsoft.JSInterop;

namespace dkgWebNode.Services
{
    public class DkgWebNodeService(IJSRuntime jsRuntime, ILogger<DkgWebNodeService> logger)
    {
        public const int MinimalPollingInterval = 3000;
        public const string DefaultServiceNodeUrl = "http://localhost:8080";
        public const string DefaultName = "Dkg Web Node";

        private const string _serviceNodeUrlKey = "ServiceNodeUrl";
        private const string _nameKey = "Name";
        private const string _pollingIntervalKey = "PollingInterval";

        private readonly IJSRuntime _jsRuntime = jsRuntime;
        private readonly ILogger<DkgWebNodeService> _logger = logger;

        private string? _address = null;
        private string? _privateKey = null;
        private string? _name = null;
        private string? _serviceNodeUrl = null;
        private int _pollingInterval = 3000;

        public event Action? OnChange;
        private void NotifyStateChanged() => OnChange?.Invoke();
        public void SetKeys(string privateKey, string publicKey)
        {
            _privateKey = privateKey;
            _address = publicKey;
            NotifyStateChanged();
        }
        public void ClearKeys()
        {
            _privateKey = null;
            _address = null;
            NotifyStateChanged();
        }
        public bool HasKeys()
        {
            return _address is not null && _privateKey is not null;
        }

        public async Task InitNodeConfig()
        {
            string? tmpString = await _jsRuntime.InvokeAsync<string?>("getFromLocalStorage", _serviceNodeUrlKey);
            _serviceNodeUrl = tmpString ?? DefaultServiceNodeUrl;

            tmpString = await _jsRuntime.InvokeAsync<string?>("getFromLocalStorage", _nameKey);
            _name = tmpString ?? DefaultName;

            tmpString = await _jsRuntime.InvokeAsync<string?>("getFromLocalStorage", _pollingIntervalKey);
            int tmpInt = tmpString is not null ? int.Parse(tmpString) : 0;
            _pollingInterval = tmpInt > MinimalPollingInterval ? tmpInt : MinimalPollingInterval;

            _logger.LogInformation("DkgWebNodeService initialized. Service node url: {_serviceNodeUrl}, name: '{_name}', polling interval: {_pollingInterval}", 
                                    _serviceNodeUrl, _name, _pollingInterval);
        }

        public async Task SaveNodeConfig()
        {
            await _jsRuntime.InvokeVoidAsync("saveToLocalStorage", _serviceNodeUrlKey, _serviceNodeUrl);
            await _jsRuntime.InvokeVoidAsync("saveToLocalStorage", _nameKey, _name);
            await _jsRuntime.InvokeVoidAsync("saveToLocalStorage", _pollingIntervalKey, _pollingInterval.ToString());

            _logger.LogInformation("DkgWebNodeService configuration saved. Service node url: {_serviceNodeUrl}, name: '{_name}', polling interval: {_pollingInterval}",
                                    _serviceNodeUrl, _name, _pollingInterval);
        }
        public DkgNodeConfig GetNodeConfig()
        {
            return new DkgNodeConfig
            {
                NiceName = _name,
                Address = _address ?? "",
                PrivateKey = _privateKey ?? "",
                ServiceNodeUrl = _serviceNodeUrl ?? "",
                PollingInterval = _pollingInterval
            };
        }

        public void SetNodeConfig(DkgNodeConfig config)
        {
            if (config.NiceName != "")
            {
                _name = config.NiceName;
            }
            if (config.PublicKey is not null && config.PublicKey != "")
            {
                _address = config.Address;
            }
            if (config.PrivateKey != "")
            {
                _privateKey = config.PrivateKey;
            }
            if (config.ServiceNodeUrl != "")
            {
                _serviceNodeUrl = config.ServiceNodeUrl;
            }
            if (config.PollingInterval > MinimalPollingInterval)
            {
                _pollingInterval = config.PollingInterval;
            }
            _pollingInterval = config.PollingInterval;

            _logger.LogInformation("DkgWebNodeService configuration updated. Service node url: {_serviceNodeUrl}, name: '{_name}', polling interval: {_pollingInterval}",
                                    _serviceNodeUrl, _name, _pollingInterval); 
            NotifyStateChanged();
        }
    }
}
