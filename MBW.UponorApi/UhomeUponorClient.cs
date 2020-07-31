using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MBW.UponorApi.Configuration;
using MBW.UponorApi.Enums;
using MBW.UponorApi.Objects;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace MBW.UponorApi
{
    public class UhomeUponorClient
    {
        private const int PropertiesPerRequest = 50;
        private const int MaxControllers = 4;
        private const int MaxThermostats = 12;

        private readonly HttpClient _httpClient;
        private readonly UponorConfiguration _configuration;

        private int _nextRequestInteger = 1;

        public event Func<Task> OnSuccessfulResponse;
        public event Func<string, Task> OnFailedResponse;

        public UhomeUponorClient(HttpClient httpClient, IOptions<UponorConfiguration> configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration.Value;

            _httpClient.BaseAddress = _configuration.Host;
        }

        private async Task<HttpResponseMessage> SendRequest(HttpRequestMessage requestMessage, CancellationToken token)
        {
            try
            {
                HttpResponseMessage res = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseContentRead, token);

                if (res.IsSuccessStatusCode)
                {
                    Task tsk = OnSuccessfulResponse?.Invoke();
                    if (tsk != null)
                        await tsk;
                }
                else
                {
                    Task tsk = OnFailedResponse?.Invoke($"API returned HTTP code {res.StatusCode}");
                    if (tsk != null)
                        await tsk;
                }

                return res;
            }
            catch (Exception e)
            {
                Task tsk = OnFailedResponse?.Invoke($"Error occurred: {e.Message}");
                if (tsk != null)
                    await tsk;

                throw;
            }
        }

        protected async Task<UponorWhoiseResponse> GetWhois(CancellationToken token)
        {
            UponorRequest request = new UponorRequest();
            request.Method = "whois";
            request.Id = Interlocked.Increment(ref _nextRequestInteger);

            string json = JsonConvert.SerializeObject(request);

            using HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, "api");
            requestMessage.Content = new StringContent(json);

            HttpResponseMessage res = await SendRequest(requestMessage, token);
            string resJson = res.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();

            UponorResponse<UponorWhoiseResponse> resp = JsonConvert.DeserializeObject<UponorResponse<UponorWhoiseResponse>>(resJson);

            return resp.Result;
        }

        protected async Task<object> GetAlarms(CancellationToken token = default)
        {
            throw new NotImplementedException();

            UponorRequest request = new UponorRequest();
            request.Method = "readactivealarms";
            request.Id = Interlocked.Increment(ref _nextRequestInteger);

            string json = JsonConvert.SerializeObject(request);

            using HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, "api");
            requestMessage.Content = new StringContent(json);

            HttpResponseMessage res = await SendRequest(requestMessage, token);
            string resJson = res.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();

            // JS:
            //for (var i = 0; i < resArr.objects.length; i++) {
            //    var obj = resArr.objects[i];
            //    var state = obj.properties[self._alarmPropVal.state];
            //    var acked = obj.properties[self._alarmPropVal.acked];
            //    var name = obj.properties[self._alarmPropVal.name];
            //    values[obj.id] = {
            //        state: state && state.value > 0,
            //        acked: acked && acked.value > 0,
            //        name: name && name.value
            //    }
            //}

            // Other alarms, battery etc.
            //var alarms = [];
            //if (utils.isAlarmActive(c.api.zoneAlarm(unitNo, zoneNo, 'battery_alarm'))) {
            //    alarms.push('images img-alarm-batt');
            //}
            //if (utils.isAlarmActive(c.api.zoneAlarm(unitNo, zoneNo, 'rf_alarm'))) {
            //    alarms.push('images img-alarm-signal');
            //}
            //if (utils.isAlarmActive(c.api.zoneAlarm(unitNo, zoneNo, 'technical_alarm'))) {
            //    alarms.push('images img-alarm-tech');
            //}
            //if (utils.isAlarmActive(c.api.zoneAlarm(unitNo, zoneNo, 'tamper_indication'))) {
            //    alarms.push('images img-alarm-tech');
            //}

            //// add alarm row if we have active alarms of rh protection is active
            //if (alarms.length > 0) {
            //    var tr = addRow(tbRooms);
            //    tr.addClass('clickable_alarm_row');
            //    addCell(tr, {text: c.api.zoneValue(unitNo, zoneNo, 'room_name')});
            //    addCell(tr, {icons: alarms});

            //    var td = $C('td').attr('align', 'right').appendTo(tr);
            //        $C('i').addClass('images img-arrow-right').appendTo(td);
            //    addCell(tr, {element: td});

            //    tr.click(function () {
            //        a.openScreenUZ(unitNo, zoneNo, screens.alarms);
            //    });
            //}

            UponorResponse<UponorWhoiseResponse> resp = JsonConvert.DeserializeObject<UponorResponse<UponorWhoiseResponse>>(resJson);

            return resp.Result;
        }

        public async Task<UponorResponseContainer> ReadValues(IEnumerable<int> objects, IEnumerable<UponorProperties> properties, CancellationToken token = default)
        {
            ICollection<UponorProperties> propsList = properties as ICollection<UponorProperties> ?? properties.ToList();

            List<List<int>> batches = new List<List<int>>();
            List<int> current = new List<int>();
            batches.Add(current);

            int objectsPerRequest = (int)Math.Max(1, Math.Round(PropertiesPerRequest * 1f / propsList.Count));

            foreach (int o in objects)
            {
                current.Add(o);
                if (current.Count >= objectsPerRequest)
                    batches.Add(current = new List<int>());
            }

            UponorResponseContainer responseContainer = new UponorResponseContainer();
            List<Task<HttpResponseMessage>> tasks = new List<Task<HttpResponseMessage>>();

            if (batches.All(x => !x.Any()))
            {
                // Early exit, if no requests are to be made
                return responseContainer;
            }

            foreach (List<int> batch in batches)
            {
                UponorRequest request = new UponorRequest();
                request.Id = Interlocked.Increment(ref _nextRequestInteger);

                foreach (int objectId in batch)
                {
                    UponorObject obj = new UponorObject();
                    obj.Id = objectId.ToString();

                    foreach (UponorProperties property in propsList)
                        obj.Properties.Add((int)property, UponorValueContainer.EmptyValueContainer);

                    request.Params.Objects.Add(obj);
                }

                string json = JsonConvert.SerializeObject(request);

                HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, "api");
                requestMessage.Content = new StringContent(json);

                Task<HttpResponseMessage> requestTask = SendRequest(requestMessage, token);
                tasks.Add(requestTask);
            }

            // Await each response
            foreach (Task<HttpResponseMessage> task in tasks)
            {
                HttpResponseMessage res = await task;
                string resJson = res.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                // Hack json to handle empty properties
                resJson = resJson.Replace(":}", ":\"\"}");

                UponorResponse<UponorParams> resp = JsonConvert.DeserializeObject<UponorResponse<UponorParams>>(resJson);

                foreach (UponorObject o in resp.Result.Objects)
                    foreach (KeyValuePair<int, UponorValueContainer> prop in o.Properties)
                        responseContainer.AddResponse(Convert.ToInt32(o.Id), (UponorProperties)prop.Key, prop.Value.Value);
            }

            return responseContainer;
        }

        public async Task SetValues(IEnumerable<(int @object, UponorProperties property, object value)> values, CancellationToken token = default)
        {
            UponorRequest request = new UponorRequest();
            request.Id = Interlocked.Increment(ref _nextRequestInteger);
            request.Method = "write";

            Dictionary<int, UponorObject> objects = new Dictionary<int, UponorObject>();

            foreach ((int @object, UponorProperties property, object value) in values)
            {
                if (!objects.TryGetValue(@object, out UponorObject obj))
                {
                    objects[@object] = obj = new UponorObject
                    {
                        Id = @object.ToString()
                    };
                    request.Params.Objects.Add(obj);
                }

                obj.Properties[(int)property] = new UponorValueContainer(value);
            }

            string json = JsonConvert.SerializeObject(request);

            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, "api");
            requestMessage.Content = new StringContent(json);

            HttpResponseMessage resp = await SendRequest(requestMessage, token);

            resp.EnsureSuccessStatusCode();
        }

        public async Task<UponorResponseContainer> GetAll(CancellationToken token = default)
        {
            IEnumerable<int> objs = Enumerable.Range(0, 2060);
            IEnumerable<UponorProperties> props = EnumsNET.Enums.GetValues<UponorProperties>();

            //objs = Enumerable.Range(0, 80);
            //objs = new[]
            //{
            //    UponorObjects.Thermostat(UponorThermostats.RfAlarm, 0, 0),
            //    UponorObjects.Thermostat(UponorThermostats.BatteryAlarm, 0, 0),
            //    UponorObjects.Thermostat(UponorThermostats.TechnicalAlarm, 0, 0),
            //    UponorObjects.Thermostat(UponorThermostats.TamperIndication, 0, 0)
            //};
            //objs = Enumerable.Range(0, 4).SelectMany(c => Enumerable.Range(0, 12).SelectMany(t => new[]
            //{
            //    UponorObjects.Thermostat(UponorThermostats.RfAlarm, c, t),
            //    UponorObjects.Thermostat(UponorThermostats.BatteryAlarm, c, t),
            //    UponorObjects.Thermostat(UponorThermostats.TechnicalAlarm, c, t),
            //    UponorObjects.Thermostat(UponorThermostats.TamperIndication, c, t)
            //}));

            //props = new[] { UponorProperties.Name, UponorProperties.Value, UponorProperties.ObjectId };
            //props = Enumerable.Range(0, 1000).Select(s => (UponorProperties)s);

            UponorResponseContainer res = await ReadValues(objs, props, token);

            return res;
        }

        public async Task<SystemProperties> GetSystemInfo(CancellationToken token = default)
        {
            // Discover all thermostats and controllers enabled
            int[] objs = {
                UponorObjects.System(UponorSystem.ControllerPresence),
                UponorObjects.Controller(UponorController.ThermostatPresence, 0),
                UponorObjects.Controller(UponorController.ThermostatPresence, 1),
                UponorObjects.Controller(UponorController.ThermostatPresence, 2),
                UponorObjects.Controller(UponorController.ThermostatPresence, 3),
                UponorObjects.System(UponorSystem.HcMode)
            };

            Task<UponorWhoiseResponse> whoisTask = GetWhois(token);

            IEnumerable<UponorProperties> props = new[] { UponorProperties.Value };
            UponorResponseContainer values = await ReadValues(objs, props, token);

            if (!values.TryGetValue(UponorObjects.System(UponorSystem.ControllerPresence),
                UponorProperties.Value, out int val))
                throw new Exception("Unable to detect present controllers");

            SystemProperties result = new SystemProperties();
            List<int> tmpList = new List<int>();

            // Controllers
            for (byte i = 1; i <= MaxControllers; i++)
            {
                if ((val & (1 << (i - 1))) != 0)
                    tmpList.Add(i);
            }

            result.AvailableControllers = tmpList.ToArray();
            result.AvailableThermostats = new int[MaxControllers + 1][];
            tmpList.Clear();

            // Thermostats
            foreach (int controller in result.AvailableControllers)
            {
                if (!values.TryGetValue(UponorObjects.Controller(UponorController.ThermostatPresence, controller),
                    UponorProperties.Value, out val))
                    throw new Exception($"Unable to detect present thermostats for {controller}");

                for (int i = 1; i <= MaxThermostats; i++)
                {
                    if ((val & (1 << (i - 1))) != 0)
                        tmpList.Add(i);
                }

                result.AvailableThermostats[controller] = tmpList.ToArray();
                tmpList.Clear();
            }

            // System details
            result.System = await whoisTask;

            // H/C Mode
            if (values.TryGetValue(UponorObjects.System(UponorSystem.HcMode),
                UponorProperties.Value, out val))
            {
                if (val > 0)
                    result.HcMode = HcMode.Cooling;
                else
                    result.HcMode = HcMode.Heating;
            }

            return result;
        }
    }
}
