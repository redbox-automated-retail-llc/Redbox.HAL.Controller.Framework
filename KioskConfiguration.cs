using Redbox.HAL.Component.Model;
using Redbox.HAL.Core;
using System;
using System.IO;
using System.Xml.Serialization;

namespace Redbox.HAL.Controller.Framework
{
    public sealed class KioskConfiguration
    {
        private bool _loaded;
        private static XmlSerializer _serializer = new XmlSerializer(typeof(KioskConfiguration));
        private const string FILE_NAME = "Kiosk.xml";
        private const string IRHardwareInstallDateName = "IRHardwareInstallDate";

        public static KioskConfiguration Instance => Singleton<KioskConfiguration>.Instance;

        public void Initialize(ErrorList errors)
        {
            if (this.Load())
                return;
            errors.Add(Error.NewError("KCL001", "KioskConfiguration Initialize failed to load.", ""));
        }

        public DateTime? IRHardwareInstallDate { get; set; }

        public void SetConfig(string name, string value, bool saveValue = false)
        {
            LogHelper.Instance.Log(string.Format("KioskConfiguration.SetConfig - name: {0}, value: {1}, save: {2}", (object)name, (object)value, (object)saveValue));
            if (name == "IRHardwareInstallDate")
            {
                DateTime result;
                this.IRHardwareInstallDate = string.IsNullOrEmpty(value) || !DateTime.TryParse(value, out result) ? new DateTime?() : new DateTime?(result);
            }
            else
                LogHelper.Instance.Log(string.Format("KioskConfiguration.SetConfig doesn't support: {0} - {1}", (object)name, (object)value));
            if (!saveValue)
                return;
            this.Save();
        }

        private void Save()
        {
            LogHelper.Instance.Log("KioskConfiguration.Save - " + this.FilePath);
            try
            {
                using (StreamWriter streamWriter = new StreamWriter(this.FilePath))
                    KioskConfiguration._serializer.Serialize((TextWriter)streamWriter, (object)KioskConfiguration.Instance);
            }
            catch (Exception ex)
            {
                LogHelper.Instance.Log(string.Format("Exception when Saving KioskConfiguration: {0}", (object)ex));
            }
        }

        public bool Load()
        {
            try
            {
                this._loaded = false;
                if (File.Exists(this.FilePath))
                {
                    KioskConfiguration kioskConfiguration = (KioskConfiguration)null;
                    using (StreamReader streamReader = new StreamReader(this.FilePath))
                        kioskConfiguration = KioskConfiguration._serializer.Deserialize((TextReader)streamReader) as KioskConfiguration;
                    if (kioskConfiguration == null)
                        return false;
                    this.IRHardwareInstallDate = kioskConfiguration.IRHardwareInstallDate;
                    this._loaded = true;
                }
                else
                {
                    this._loaded = true;
                    this.Save();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Instance.Log(string.Format("Exception when Loading KioskConfiguration: {0}", (object)ex));
                this._loaded = false;
            }
            finally
            {
                LogHelper.Instance.Log(string.Format("KioskConfiguration.Load File: {0} - Success: {1}", (object)this.FilePath, (object)this._loaded.ToString()));
            }
            return this._loaded;
        }

        private KioskConfiguration()
        {
        }

        private string FilePath
        {
            get
            {
                IRuntimeService service = ServiceLocator.Instance.GetService<IRuntimeService>();
                return service != null ? service.RuntimePath("Kiosk.xml") : Path.Combine(Path.GetDirectoryName(typeof(KioskConfiguration).Assembly.Location), "Kiosk.xml");
            }
        }
    }
}
