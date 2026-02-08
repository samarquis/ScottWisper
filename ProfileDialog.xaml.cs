using WhisperKey.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WhisperKey
{
    public partial class ProfileDialog : Window
    {
        public HotkeyProfile Profile { get; private set; }

        public ProfileDialog(string title, HotkeyProfile? profile = null)
        {
            InitializeComponent();
            Title = title;
            
            Profile = profile ?? new HotkeyProfile 
            { 
                Id = Guid.NewGuid().ToString("N")[..8],
                Name = "New Profile",
                Description = "Custom hotkey profile",
                CreatedAt = DateTime.Now,
                ModifiedAt = DateTime.Now,
                Version = "1.0"
            };
            
            InitializeDialog();
        }

        private void InitializeDialog()
        {
            // Set up data binding
            DataContext = new ProfileViewModel(Profile);
            
            // Set initial values
            ProfileNameTextBox.Text = Profile.Name;
            ProfileDescriptionTextBox.Text = Profile.Description;
            
            // Set category
            var category = Profile.Metadata?.Category ?? "General";
            foreach (ComboBoxItem item in ProfileCategoryComboBox.Items)
            {
                if (item.Tag?.ToString() == category)
                {
                    ProfileCategoryComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Set checkboxes
            IsDefaultCheckBox.IsChecked = Profile.IsDefault;
            IsReadonlyCheckBox.IsChecked = Profile.IsReadonly;
            
            // Set tags
            if (Profile.Tags != null)
            {
                ProfileTagsTextBox.Text = string.Join(", ", Profile.Tags);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(ProfileNameTextBox.Text))
            {
                MessageBox.Show("Profile name is required.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Update profile with dialog values
            Profile.Name = ProfileNameTextBox.Text.Trim();
            Profile.Description = ProfileDescriptionTextBox.Text.Trim();
            Profile.IsDefault = IsDefaultCheckBox.IsChecked == true;
            Profile.IsReadonly = IsReadonlyCheckBox.IsChecked == true;
            Profile.ModifiedAt = DateTime.Now;
            
            // Update category
            if (ProfileCategoryComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                Profile.Metadata ??= new ProfileMetadata();
                Profile.Metadata.Category = selectedItem.Tag?.ToString() ?? "General";
            }
            
            // Update tags
            if (!string.IsNullOrWhiteSpace(ProfileTagsTextBox.Text))
            {
                var tags = ProfileTagsTextBox.Text
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(tag => tag.Trim())
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .ToList();
                
                Profile.Tags = tags;
            }
            else
            {
                Profile.Tags = new List<string>();
            }

            DialogResult = true;
            Close();
        }
    }

    public class ProfileViewModel
    {
        public HotkeyProfile Profile { get; }

        public ProfileViewModel(HotkeyProfile profile)
        {
            Profile = profile;
        }

        public string Name
        {
            get => Profile.Name;
            set => Profile.Name = value;
        }

        public string Description
        {
            get => Profile.Description;
            set => Profile.Description = value;
        }

        public string TagsString
        {
            get => Profile.Tags != null ? string.Join(", ", Profile.Tags) : string.Empty;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    Profile.Tags = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(tag => tag.Trim())
                        .Where(tag => !string.IsNullOrWhiteSpace(tag))
                        .ToList();
                }
                else
                {
                    Profile.Tags = new List<string>();
                }
            }
        }
    }
}
