namespace OscMonitor;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell()) { Title = "OSC Monitor", Width = 900, Height = 700 };
	}
}
