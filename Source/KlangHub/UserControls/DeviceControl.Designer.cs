namespace KlangHub.UserControls
{
    partial class DeviceControl
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                uiTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            this.AllowDrop = true;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = KlangHub.Classes.Theme.Ink;
            this.Margin = new System.Windows.Forms.Padding(7);
            this.Name = "DeviceControl";
            this.Size = new System.Drawing.Size(322, 152);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.DeviceControl_DragDrop);
            this.DragOver += new System.Windows.Forms.DragEventHandler(this.DeviceControl_DragOver);
            this.ResumeLayout(false);
        }
        #endregion

        private System.Windows.Forms.ToolTip toolTip;
    }
}
