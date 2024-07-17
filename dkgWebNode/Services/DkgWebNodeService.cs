using dkgNode.Models;
using dkgNode.Services;
using Microsoft.JSInterop;
using dkgWebNode.Models;
using static dkgCommon.Constants.NStatus;
using Microsoft.Extensions.Logging;
using Solnet.Wallet;

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

        private CancellationTokenSource _cancellationTokenSource = new();

        private DkgNodeService? _dkgNodeService = null;

        public event Action? OnChange;
        public event Action? OnDkgChange;
        private void NotifyStateChanged() => OnChange?.Invoke();
        private void NotifyDkgStateChanged() => OnDkgChange?.Invoke();
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
                PrivateKey = _privateKey ?? "" ,
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
            if (config.Address != "")
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

        public async void RunDkg()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            DkgNodeConfig nodeConfig = GetNodeConfig();
            _logger.LogDebug("Starting Dkg node with private key '{key}', address '{address}'", nodeConfig.PrivateKey, nodeConfig.Address);

            _dkgNodeService = new DkgNodeService(nodeConfig, _logger);
            _dkgNodeService.OnDkgChange += OnDkgStateChanged;
            await RunDkgUntilCancelled(_cancellationTokenSource.Token);
            _dkgNodeService.OnDkgChange -= OnDkgStateChanged;
            _dkgNodeService = null;
            NotifyDkgStateChanged();
        }

        private void OnDkgStateChanged()
        {
            NotifyDkgStateChanged();
        }
        public void CancelDkg()
        {
            _cancellationTokenSource.Cancel();
        }

        public DkgStatus GetDkgStatus()
        {   
            return new DkgStatus()
            {
                NodeStatus = _dkgNodeService?.GetStatus().ToString() ?? "Stopped",
                RoundId = _dkgNodeService?.GetRound()?.ToString() ?? "N/A",
                RoundStatus = _dkgNodeService?.GetRoundStatus().ToString() ?? "N/A",

                LastRoundId = _dkgNodeService?.GetLastRound()?.ToString() ?? "N/A",
                LastRoundStatus = _dkgNodeService?.GetLastRoundStatus().ToString() ?? "N/A",
                LastRoundResult = _dkgNodeService?.GetLastRoundResult().ToString() ?? "N/A",
                LastNodeStatus = _dkgNodeService?.GetLastNodeStatus().ToString() ?? "N/A",
                LastNodeRandom = _dkgNodeService?.GetLastNodeRandom().ToString() ?? "N/A"
            };
        }
        private async Task RunDkgUntilCancelled(CancellationToken cancellationToken)
        {
            try
            {
                var httpClient = new HttpClient();
                while (!cancellationToken.IsCancellationRequested && _dkgNodeService is not null)
                {
                    if (_dkgNodeService.GetStatus() == NotRegistered)
                    {
                        await _dkgNodeService.Register(httpClient);
                    }

                    var statusResponse = await _dkgNodeService.ReportStatus(httpClient, null);

                    if (_dkgNodeService.GetStatus() == RunningStepOne)
                    {
                        await _dkgNodeService.RunDkg(httpClient, statusResponse.Data, cancellationToken);
                        _dkgNodeService.UpdateKeys();
                    }
                    else
                    {
                        await Task.Delay(_pollingInterval, cancellationToken);
                    }
                }                
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Dkg Web Node was stopped.");
            }
            catch(Exception ex)
            {
                _logger.LogInformation("Dkg Web Node was terminated by error '{msg}'.", ex.Message);
            }
            finally
            {
                // Clean up resources, if necessary
                _logger.LogInformation("Cleaning up resources.");
            }
        }

    }
}
