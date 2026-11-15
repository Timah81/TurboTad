using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TurboTad
{
    internal static class InputNames
    {
        internal static string Describe(InputSource source)
        {
            if (source == null) return "Unassigned";
            if (source.Kind == "key") return "Key " + ((Keys)source.Code).ToString();
            string[] parts = source.Device.Split(new char[]{':'},4);
            string name = parts.Length == 4 ? "#" + (UInt32.Parse(parts[0])+1) + " " + parts[3] : source.Device;
            return name + (source.Kind == "axis" ? " / Axis " : " / Button ") + (source.Code+1);
        }
    }

    internal sealed class InputCaptureDialog : Form
    {
        private readonly IList<Device> devices;
        private readonly Dictionary<string,bool> previous = new Dictionary<string,bool>();
        private readonly Timer timer = new Timer();
        internal InputSource Selected;
        internal InputCaptureDialog(IList<Device> available)
        {
            devices = available; Text = "Assign a key or joystick button";
            ClientSize = new Size(540,150); StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            Font = new Font("Segoe UI",10F);
            Label help = new Label { Text = "Press the keyboard key or joystick button to assign.\r\nRelease held controls first. Escape cancels.", AutoSize = false };
            help.SetBounds(20,20,500,65); Controls.Add(help);
            Button cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            cancel.SetBounds(400,105,115,30);Controls.Add(cancel);CancelButton=cancel;
            Snapshot(false); timer.Interval = 16; timer.Tick += delegate { if (ContainsFocus) Snapshot(true); else Snapshot(false); };
            Shown += delegate { Snapshot(false); timer.Start(); };
            FormClosed += delegate { timer.Stop(); timer.Dispose(); };
        }
        private void Observe(InputSource source, bool down, bool capture)
        {
            bool before;
            if (!previous.TryGetValue(source.Identity,out before)) before = true;
            previous[source.Identity]=down;
            if (capture && down && !before && Selected == null)
            { Selected=source; DialogResult=DialogResult.OK; Close(); }
        }
        private void Snapshot(bool capture)
        {
            for (int key=8; key<=254; key++)
            {
                if (key==27 || key==16 || key==17 || key==18) continue;
                Observe(InputSource.Key(key), (Native.GetAsyncKeyState(key)&0x8000)!=0, capture);
                if (Selected!=null) return;
            }
            foreach(Device d in devices)
            {
                d.Poll(); if(!d.Connected) continue;
                for(int b=0;b<Math.Min(32,(int)d.Caps.Buttons);b++)
                {
                    Observe(InputSource.Joystick(d.Identity,b,false),(d.Buttons&(1U<<b))!=0,capture);
                    if(Selected!=null)return;
                }
            }
        }
    }

    internal sealed class DirectionPairDialog : Form
    {
        private InputSource negative,positive;
        private readonly Button negativeButton=new Button(),positiveButton=new Button();
        internal AxisBinding Binding;
        internal DirectionPairDialog(string axisName,string negativeName,string positiveName,AxisBinding current,IList<Device> devices)
        {
            Text="TurboTad — "+axisName; ClientSize=new Size(650,245);
            StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
            Font=new Font("Segoe UI",10F); BackColor=Color.FromArgb(37,40,44);ForeColor=Color.WhiteSmoke;
            negative=current.Negative;positive=current.Positive;
            Label help=new Label {Text="Assign each direction independently. Both held = neutral. Release = neutral."};
            help.SetBounds(18,15,610,42);Controls.Add(help);
            Label n=new Label {Text=negativeName+"  (−)"},p=new Label{Text=positiveName+"  (+)"};
            n.SetBounds(18,73,160,28);p.SetBounds(18,123,160,28);Controls.Add(n);Controls.Add(p);
            negativeButton.SetBounds(180,67,445,36);positiveButton.SetBounds(180,117,445,36);
            Controls.Add(negativeButton);Controls.Add(positiveButton);
            negativeButton.Click+=delegate { using(InputCaptureDialog d=new InputCaptureDialog(devices)) if(d.ShowDialog(this)==DialogResult.OK){negative=d.Selected;RefreshNames();} };
            positiveButton.Click+=delegate { using(InputCaptureDialog d=new InputCaptureDialog(devices)) if(d.ShowDialog(this)==DialogResult.OK){positive=d.Selected;RefreshNames();} };
            Button clear=new Button{Text="Clear pair"};clear.SetBounds(18,187,125,34);clear.Click+=delegate{negative=positive=null;RefreshNames();};Controls.Add(clear);
            Button cancel=new Button{Text="Cancel",DialogResult=DialogResult.Cancel};cancel.SetBounds(365,187,120,34);Controls.Add(cancel);CancelButton=cancel;
            Button save=new Button{Text="Use pair"};save.SetBounds(500,187,125,34);Controls.Add(save);
            save.Click+=delegate {
                if(negative!=null&&positive!=null&&negative.Identity==positive.Identity){MessageBox.Show(this,"Choose a different control for each direction.");return;}
                Binding=new AxisBinding{Digital=true,Negative=negative,Positive=positive};DialogResult=DialogResult.OK;Close();
            };
            RefreshNames();
        }
        private void RefreshNames(){negativeButton.Text=InputNames.Describe(negative);positiveButton.Text=InputNames.Describe(positive);}
    }
}
