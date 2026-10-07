using HyperionScreenCap.Model;
using log4net;
using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.Globalization;

namespace HyperionScreenCap
{
    public partial class ServerPropertiesForm : Form
    {
        private static readonly ILog LOG = LogManager.GetLogger(typeof(ServerPropertiesForm));

        public HyperionTaskConfiguration TaskConfiguration { get; private set; }
        public bool SaveRequested { get; private set; }
        private HyperionServer _defaultServerConfiguration;
        private bool _isHdrActive;

        public ServerPropertiesForm(HyperionTaskConfiguration taskConfiguration, bool isHdrActive)
        {
            this._defaultServerConfiguration = HyperionServer.BuildUsingDefaultFbsSettings();
            this.TaskConfiguration = taskConfiguration;
            this._isHdrActive = isHdrActive;
            InitializeComponent();
            this.Text = $"{this.Text} - {taskConfiguration.Id}";
            var protocolColumn = (DataGridViewComboBoxColumn) this.dgHyperionAddress.Columns.GetFirstColumn(DataGridViewElementStates.Visible);
            protocolColumn.DataSource = Enum.GetValues(typeof(HyperionServerProtocol));
            protocolColumn.ValueType = typeof(HyperionServerProtocol);
            InitFormFields();
        }

        private void InitFormFields()
        {
            EnableRelevantDxFields(TaskConfiguration.CaptureMethod);

            SelectValueFromComboBox(cbDx11AdapterIndex, TaskConfiguration.Dx11AdapterIndex); // TODO check item list for each combo box
            SelectValueFromComboBox(cbDx11MonitorIndex, TaskConfiguration.Dx11MonitorIndex);
            tbDx11FrameCaptureTimeout.Text = TaskConfiguration.Dx11FrameCaptureTimeout.ToString();
            SelectValueFromComboBox(cbDx11ImageScalingFactor, TaskConfiguration.Dx11ImageScalingFactor);
            tbDx11MaxFps.Text = TaskConfiguration.Dx11MaxFps.ToString();

            // HDR tone mapping fields
            chkDx11DebugCapture.Checked = TaskConfiguration.Dx11DebugCapture;
            cboDx11ToneMappingMethod.SelectedIndex = (int)TaskConfiguration.Dx11HdrToneMappingMethod;
            nudDx11HdrPeakNits.Value = Math.Max(nudDx11HdrPeakNits.Minimum,
                Math.Min(nudDx11HdrPeakNits.Maximum, TaskConfiguration.Dx11HdrPeakLuminanceNits));
            decimal saturationDecimal = (decimal)TaskConfiguration.Dx11HdrSaturation;
            nudDx11HdrSaturation.Value = saturationDecimal < nudDx11HdrSaturation.Minimum
                ? nudDx11HdrSaturation.Minimum
                : (saturationDecimal > nudDx11HdrSaturation.Maximum ? nudDx11HdrSaturation.Maximum : saturationDecimal);
            nudDx11HdrSdrWhiteNits.Value = Math.Max(nudDx11HdrSdrWhiteNits.Minimum,
                Math.Min(nudDx11HdrSdrWhiteNits.Maximum, TaskConfiguration.Dx11HdrSdrWhiteNits));
            UpdatePeakNitsEnabled();

            var hyperionServersBindingList = new BindingList<HyperionServer>(TaskConfiguration.HyperionServers);
            var hyperionServersDataSource = new BindingSource(hyperionServersBindingList, null);
            dgHyperionAddress.DataSource = hyperionServersDataSource;
        }

        private void SaveFormFields()
        {
            TaskConfiguration.CaptureMethod = CaptureMethod.DX11;
            TaskConfiguration.Dx11AdapterIndex = int.Parse(cbDx11AdapterIndex.SelectedItem.ToString());
            TaskConfiguration.Dx11MonitorIndex = int.Parse(cbDx11MonitorIndex.SelectedItem.ToString());
            TaskConfiguration.Dx11MonitorDeviceName = null;
            TaskConfiguration.Dx11FrameCaptureTimeout = int.Parse(tbDx11FrameCaptureTimeout.Text);
            TaskConfiguration.Dx11ImageScalingFactor = int.Parse(cbDx11ImageScalingFactor.SelectedItem.ToString());
            TaskConfiguration.Dx11MaxFps = int.Parse(tbDx11MaxFps.Text);

            // HDR tone mapping fields
            TaskConfiguration.Dx11HdrToneMappingMethod = (ToneMappingMethod)cboDx11ToneMappingMethod.SelectedIndex;
            TaskConfiguration.Dx11HdrPeakLuminanceNits = (int)nudDx11HdrPeakNits.Value;
            TaskConfiguration.Dx11HdrSaturation = (float)nudDx11HdrSaturation.Value;
            TaskConfiguration.Dx11HdrSdrWhiteNits = (int)nudDx11HdrSdrWhiteNits.Value;
            TaskConfiguration.Dx11DebugCapture = chkDx11DebugCapture.Checked;
        }

        private void EnableRelevantDxFields(CaptureMethod captureMethod)
        {
            rbcmDx11.Checked = true;
            grpHdrToneMapping.Enabled = _isHdrActive;
        }

        private static void SelectValueFromComboBox(ComboBox comboBox, Object value)
        {
            var valueAsString = value.ToString();
            int indexToSelect = 0;
            foreach ( object obj in comboBox.Items )
            {
                if ( obj.Equals(valueAsString) )
                {
                    comboBox.SelectedIndex = indexToSelect;
                    return;
                }
                indexToSelect++;
            }
            LOG.Error($"Unable to select value {value} from comboBox {comboBox.Name}");
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            bool validServerFound = false;
            bool invalidPriority = false;
            // Validate server rows using IP address default value
            for ( int i = 0; i < TaskConfiguration.HyperionServers.Count; i++ )
            {
                HyperionServer server = TaskConfiguration.HyperionServers[i];
                if (server.Priority < HyperionServer.MIN_PRIORITY || server.Priority > HyperionServer.MAX_PRIORITY)
                {
                    invalidPriority = true;
                }
                if ( !_defaultServerConfiguration.Host.Equals(server.Host) )
                {
                    validServerFound = true;
                    break;
                }
            }
            // Check if all rows are invalid
            if ( !validServerFound )
            {
                MessageBox.Show("All Hyperion server host names are invalid. Please sepcify a valid Hyperion server configuraion.");
                return;
            }
            if (invalidPriority)
            {
                MessageBox.Show("Invalid priority value found. Priority should be set within the range 100-199.");
                return;
            }

            TaskConfiguration.HyperionServers.RemoveAll(server => _defaultServerConfiguration.Host.Equals(server.Host));
            SaveRequested = true;
            Close();
        }

        public new void Close()
        {
            SaveFormFields();
            base.Close();
        }

        private void ServerPropertiesForm_Shown(object sender, EventArgs e)
        {
            SaveRequested = false;
        }

        private void dgHyperionAddress_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            e.Control.KeyPress -= new KeyPressEventHandler(PreventNonNumeric_KeyPressEventHandler);
            if ( dgHyperionAddress.CurrentCell.ColumnIndex == dgHyperionAddress.Columns["clmnPort"].Index
                || dgHyperionAddress.CurrentCell.ColumnIndex == dgHyperionAddress.Columns["clmnPriority"].Index
                || dgHyperionAddress.CurrentCell.ColumnIndex == dgHyperionAddress.Columns["clmnMessageDuration"].Index)
            {
                e.Control.KeyPress += new KeyPressEventHandler(PreventNonNumeric_KeyPressEventHandler);
            }
        }

        private void dgHyperionAddress_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == dgHyperionAddress.Columns["clmnProtocol"].Index)
            {
                var columnValue = dgHyperionAddress.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
                if (columnValue != null)
                {
                    var serverProtocol = (HyperionServerProtocol) columnValue;
                    int newPortValue = -1;
                    switch (serverProtocol)
                    {
                        case HyperionServerProtocol.FLAT_BUFFERS:
                            newPortValue = HyperionServer.BuildUsingDefaultFbsSettings().Port;
                            break;

                        default:
                            throw new NotImplementedException($"Hyperion server protocol {serverProtocol} is not supported yet");
                    }
                    dgHyperionAddress.Rows[e.RowIndex].Cells["clmnPort"].Value = newPortValue; // Change port according to protocol
                }
            }
        }

        private void PreventNonNumeric_KeyPressEventHandler(object sender, KeyPressEventArgs e)
        {
            if ( !char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) )
            {
                e.Handled = true;
            }
        }

        private void dgHyperionAddress_DefaultValuesNeeded(object sender, DataGridViewRowEventArgs e)
        {
            e.Row.Cells[0].Value = _defaultServerConfiguration.Protocol;
            e.Row.Cells[1].Value = _defaultServerConfiguration.Host;
            e.Row.Cells[2].Value = _defaultServerConfiguration.Port;
            e.Row.Cells[3].Value = _defaultServerConfiguration.Priority;
            e.Row.Cells[4].Value = _defaultServerConfiguration.MessageDuration;
        }

        private void UpdatePeakNitsEnabled()
        {
            bool enabled = cboDx11ToneMappingMethod.SelectedIndex == (int)ToneMappingMethod.ReinhardExtended;
            nudDx11HdrPeakNits.Enabled = enabled;
            lblDx11HdrPeakNits.Enabled = enabled;
        }

        private void cboDx11ToneMappingMethod_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePeakNitsEnabled();
        }

        private void rbcmDx11_CheckedChanged(object sender, EventArgs e)
        {
            EnableRelevantDxFields(CaptureMethod.DX11);
        }
    }
}
