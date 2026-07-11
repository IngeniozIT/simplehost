using System.Windows;
using SimpleHost.Presentation.ViewModels;

namespace SimpleHost.Presentation;

/// <summary>
/// The modal Subscriptions dialog, opened from the Subscriptions menu. It binds to the shared
/// <see cref="MainViewModel"/>, so subscribing to a remote list, refreshing one, or refreshing all
/// drive the same view-model methods as the former Subscriptions tab, and their results show in the
/// bound list.
/// </summary>
public partial class SubscriptionsWindow : Window
{
    private readonly MainViewModel _viewModel;

    public SubscriptionsWindow(MainViewModel viewModel)
    {
        System.ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnSubscribeClick(object sender, RoutedEventArgs e)
        => _viewModel.SubscribeToRemoteList(SubscriptionUrlBox.Text.Trim(), SubscriptionCategoryBox.Text.Trim());

    private void OnRefreshSubscriptionClick(object sender, RoutedEventArgs e)
        => _viewModel.RefreshSubscription(SubscriptionUrlBox.Text.Trim(), SubscriptionCategoryBox.Text.Trim());

    private void OnRefreshAllSubscriptionsClick(object sender, RoutedEventArgs e)
        => _viewModel.RefreshAllSubscriptions();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
