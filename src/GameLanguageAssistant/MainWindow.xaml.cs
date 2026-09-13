using System.Windows;

namespace GameLanguageAssistant;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ShowExample_Click(object sender, RoutedEventArgs e)
    {
        // 첫 실습: AI 호출 없이 준비된 문장을 화면에 표시합니다.
        EnglishText.Text = "I'll come with you.";
        PronunciationText.Text = "아일 컴 위드 유";
    }
}
