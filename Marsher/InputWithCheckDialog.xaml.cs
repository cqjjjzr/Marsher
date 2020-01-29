using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using MahApps.Metro.Controls.Dialogs;
using Marsher.Annotations;
using Unclassified.TxLib;

namespace Marsher
{
    public partial class InputWithCheckDialog
    {
        private readonly InputWithCheckDialogViewModel _viewModel;

        public InputWithCheckDialog(string title, string message, string defaultValue, Func<string, string> checkValidFunc)
        {
            InitializeComponent();
            Title = title;
            _viewModel = new InputWithCheckDialogViewModel(message, checkValidFunc)
            {
                Input = defaultValue
            };

            DataContext = _viewModel;
        }

        public Task<Tuple<MessageDialogResult, string>> WaitUntilButton()
        {
            var result = new TaskCompletionSource<Tuple<MessageDialogResult, string>>();
            OKButton.Click += (sender, args) => result.TrySetResult(new Tuple<MessageDialogResult, string>(MessageDialogResult.Affirmative, _viewModel.Input));
            CancelButton.Click += (sender, args) => result.TrySetResult(new Tuple<MessageDialogResult, string>(MessageDialogResult.Negative, null));

            return result.Task;
        }

        private void BaseMetroDialog_Loaded(object sender, RoutedEventArgs e)
        {
            OKButton.Focus();
        }
    }

    public class InputWithCheckDialogViewModel : INotifyPropertyChanged
    {
        private readonly Func<string, string> _checkValidFunc;
        private string _errorMessage;
        private bool _validInput;
        private string _input;

        public InputWithCheckDialogViewModel(string message, Func<string, string> checkValidFunc)
        {
            _checkValidFunc = checkValidFunc;
            Message = message;
        }
        public string Message { get; }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();

                ChangeErrorState();
            }
        }

        public bool ValidInput
        {
            get => _validInput;
            set
            {
                _validInput = value;
                OnPropertyChanged();
            }
        }

        private void ChangeErrorState()
        {
            ValidInput = string.IsNullOrWhiteSpace(ErrorMessage);
        }

        public string Input
        {
            get => _input;
            set
            {
                _input = value;
                OnPropertyChanged();

                ErrorMessage = _checkValidFunc(_input);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
