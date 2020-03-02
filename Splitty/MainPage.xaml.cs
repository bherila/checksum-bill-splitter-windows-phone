using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Splitty.Resources;

namespace Splitty
{
	public partial class MainPage : PhoneApplicationPage
	{
		// Constructor
		public MainPage()
		{
			InitializeComponent();

			// Sample code to localize the ApplicationBar
			//BuildLocalizedApplicationBar();

			e15.Click += (o, e) => { txtTipPercentage.Text = "15"; };
			e18.Click += (o, e) => { txtTipPercentage.Text = "18"; };
			e20.Click += (o, e) => { txtTipPercentage.Text = "20"; };

			txtTipPercentage.TextChanged += (o, e) => calc();
			txtTaxAmount.TextChanged += (o, e) => calc();
			btnPeopleNext.Click += (o, e) => pivot.SelectedIndex++;

			var numericScope = new InputScope();
			var numericScopeName = new InputScopeName();
			numericScopeName.NameValue = InputScopeNameValue.NumberFullWidth;
			numericScope.Names.Add(numericScopeName);

			txtAmt.InputScope
				= txtCreditCost.InputScope
					= txtCreditValue.InputScope
						= txtTaxAmount.InputScope
							= txtTipPercentage.InputScope
								= numericScope;

			// add 2 people by default
			Button_Click(null, null);
			Button_Click(null, null);
		}

		public class Allocation
		{
			public double amt;
			public List<PersonItem> person;
		}

		List<Allocation> billItems = new List<Allocation>();
		List<Allocation> giftCardPrices = new List<Allocation>();
		List<Allocation> giftCardValues = new List<Allocation>();

		void calc()
		{
			people.ForEach(p => p.AmtDue = 0); // reset amts due & tip

			// add up everyone's contributions for the stuff they bought
			foreach (var billItem in billItems)
			{
				foreach (var person in billItem.person)
					person.AmtDue += (billItem.amt / (double)billItem.person.Count);
			}
			writeAmtsDue("bill items");

			people.ForEach(p => p.TipBase = p.AmtDue); // save the baseline on which the tip will be computed

			double tax;
			if (double.TryParse(txtTaxAmount.Text, out tax))
			{
				foreach (var person in people)
					person.AmtDue += (tax/(double) people.Count);
			}
			else
			{
				tax = 0;
			}

			Debug.Assert(giftCardValues.Count == giftCardPrices.Count);
			for (int i = 0; i < giftCardPrices.Count; ++i)
			{
				var giftCardPrice = giftCardPrices[i];
				var giftCardValue = giftCardValues[i];

				// the cost of the gc is deducted from the amount that a purchaser owes 
				foreach (var person in giftCardPrice.person)
					person.AmtDue -= (giftCardPrice.amt / (double)giftCardPrice.person.Count);

				// now the cost is divided into everyone who's benefiting from the credit
				foreach (var person in giftCardValue.person)
					person.AmtDue += (giftCardPrice.amt / (double)giftCardValue.person.Count);

				// now the benefit of the gc is distributed across all the people
				foreach (var person in giftCardValue.person)
					person.AmtDue -= (giftCardValue.amt / (double)giftCardValue.person.Count);
			}

			people.ForEach(p => p.updateUI());

			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("{0} people are splitting {1:c} worth of items (before tax) and {2:c} of items (with tax).\n\n",
				people.Count, billItems.Sum(f => f.amt), billItems.Sum(f => f.amt) + tax);

			foreach (var person in people)
			{
				double tipAmt;
				if (double.TryParse(txtTipPercentage.Text, out tipAmt))
				{
					tipAmt /= 100.0;
					tipAmt *= person.TipBase;
				}
				sb.AppendFormat("{0} owes {1:c}. With a tip of {2}% (amounting to {3:c}), {0} would pay {4:c} in total.\n\n",
					person.Name, person.AmtDue, txtTipPercentage.Text, tipAmt, tipAmt + person.AmtDue);

				person.AllocateItemCheckbox.Content = String.Format("{0} ({1:c})", person.Name, person.AmtDue);
			}

			spSummary.Children.Clear();
			spSummary.Children.Add(new TextBlock() {Text = sb.ToString(), TextWrapping = TextWrapping.Wrap});
		}

		void writeAmtsDue(string title)
		{
			System.Diagnostics.Debug.WriteLine("================ " + title.ToUpper() + " ================");
			foreach (var p in people)
				System.Diagnostics.Debug.WriteLine(String.Format("{0} owes {1:c}", p.Name, p.AmtDue));

		}

		List<PersonItem> people = new List<PersonItem>();
		public class PersonItem
		{
			public PersonItem(MainPage page, string s1)
			{

				Grid g = new Grid();
				{
					PeopleListGridItem = g;
					g.ColumnDefinitions.Add(new ColumnDefinition() {Width = new GridLength(90.0, GridUnitType.Star)});
					g.ColumnDefinitions.Add(new ColumnDefinition() {Width = new GridLength(20.0, GridUnitType.Star)});
					g.RowDefinitions.Add(new RowDefinition());
					page.peopleList.Children.Add(g);

					NameTextbox = new TextBox();
					g.Children.Add(NameTextbox);
					Grid.SetRow(NameTextbox, 0);
					Grid.SetColumn(NameTextbox, 0);

					var b = new Button() {Content = "X"};
					b.Click += (o, e) =>
					{
						if (this.AmtDue > 0.0)
						{
							MessageBox.Show("Remove items this person owes before removing the person.");
							return;
						}
						page.peopleList.Children.Remove(g);
						page.people.Remove(this);
						page.itemListPeople.Children.Remove(this.AllocateItemCheckbox);
						page.creditListBuyers.Children.Remove(this.AllocateCreditPurchaseCheckbox);
						page.creditListBeneficiaries.Children.Remove(this.AllocateCreditCheckbox);
						page.btnPeopleNext.IsEnabled = (page.people.Count > 0);
						page.calc();
					};
					g.Children.Add(b);
					Grid.SetRow(b, 0);
					Grid.SetColumn(b, 1);
				}

				// set the text
				Name = s1;

				AllocateItemCheckbox = new CheckBox();
				AllocateItemCheckbox.Content = s1;
				page.itemListPeople.Children.Add(AllocateItemCheckbox);

				AllocateCreditPurchaseCheckbox = new CheckBox();
				AllocateCreditPurchaseCheckbox.Content = s1;
				page.creditListBuyers.Children.Add(AllocateCreditPurchaseCheckbox);

				AllocateCreditCheckbox = new CheckBox();
				AllocateCreditCheckbox.Content = s1;
				AllocateCreditCheckbox.IsChecked = true;
				page.creditListBeneficiaries.Children.Add(AllocateCreditCheckbox);

				NameTextbox.TextChanged += (o, e) => { updateUI(); };
				updateUI();

				page.calc();
			}

			public string Name
			{
				get { return NameTextbox.Text; }
				set { NameTextbox.Text = value; }
			}

			public double AmtDue;
			public double TipBase;

			public void updateUI()
			{
				AllocateCreditCheckbox.Content = AllocateCreditPurchaseCheckbox.Content = NameTextbox.Text;
			}

			public CheckBox AllocateItemCheckbox;
			public CheckBox AllocateCreditPurchaseCheckbox;
			public CheckBox AllocateCreditCheckbox;
			public TextBox NameTextbox;

			private Grid PeopleListGridItem;
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			var s1 = "Person " + (people.Count + 1).ToString();
			var pi = new PersonItem(this, s1);
			people.Add(pi);
			btnPeopleNext.IsEnabled = (people.Count > 0);
			calc();
		}

		private void amt_gotfocus(object sender, RoutedEventArgs e)
		{
			txtAmt.AcceptsReturn = false;
			if (txtAmt.Text == "Amount")
				txtAmt.Text = string.Empty;
		}

		private void btnAddItem_Click(object sender, RoutedEventArgs e)
		{
			if (people.Count == 0 || people.All(p => p.AllocateItemCheckbox.IsChecked.GetValueOrDefault(false) == false))
			{
				MessageBox.Show("You must allocate this item to at least one person.");
				return;
			}

			Allocation a = null;
			double amt;
			if (double.TryParse(txtAmt.Text, out amt))
			{
				a = new Allocation();
				a.amt = amt;
				a.person = (from x in people
					where x.AllocateItemCheckbox.IsChecked.GetValueOrDefault(false) == true
					select x).ToList();

				billItems.Add(a);

				txtAmt.Text = "";
				txtAmt.Focus();

				var b = new Button {Content = String.Format("Remove {0:c} item", amt)};
				b.Click += (o2, e2) =>
				{
					billItems.Remove(a);
					itemListItems.Children.Remove(b);
					calc();
				};
				itemListItems.Children.Add(b);

				lblAddItems.Visibility = Visibility.Collapsed;
			}
			calc();
		}

		private void btnAddCredit_Click(object  sender, RoutedEventArgs e)
		{
			double amtPaid = 0;
			double.TryParse(txtCreditCost.Text, out amtPaid);

			var a1 = new Allocation();
			var a2 = new Allocation();

			if (amtPaid > 0)
			{
				a1.person = people.Where(p => p.AllocateCreditPurchaseCheckbox.IsChecked.GetValueOrDefault(false) == true).ToList(); 
				if (people.Count == 0 || people.All(p => p.AllocateCreditPurchaseCheckbox.IsChecked.GetValueOrDefault(false) == false))
				{
					MessageBox.Show("You must allocate the purchase of this credit to at least one person, UNLESS the amount paid was 0.");
					return;
				}

				a1.amt = amtPaid;
				
			}


			double amt;
			if (double.TryParse(txtCreditValue.Text, out amt))
			{
				a2.person = people.Where(p => p.AllocateCreditCheckbox.IsChecked.GetValueOrDefault(false) == true).ToList();
				if (people.Count == 0 || people.All(p => p.AllocateCreditCheckbox.IsChecked.GetValueOrDefault(false) == false))
				{
					MessageBox.Show("You must allocate the value of this credit to at least one person.");
					return;
				}

				a2.amt = amt;
			}
			else
			{
				MessageBox.Show("Credit has no value.");
				return;
			}

			lblGroupon.Visibility = Visibility.Collapsed;

			giftCardPrices.Add(a1);
			giftCardValues.Add(a2);

			txtCreditValue.Text = txtCreditCost.Text = string.Empty;

			// add the removal button
			var b = new Button() {Content = String.Format("Remove {0:c} credit", a2.amt)};
			b.Click += (o, args) =>
			{
				creditList.Children.Remove(b); // remove the button
				giftCardPrices.Remove(a1);
				giftCardValues.Remove(a2);
				calc();
			};
			creditList.Children.Add(b);

			calc();
		}



		// Sample code for building a localized ApplicationBar
		//private void BuildLocalizedApplicationBar()
		//{
		//    // Set the page's ApplicationBar to a new instance of ApplicationBar.
		//    ApplicationBar = new ApplicationBar();

		//    // Create a new button and set the text value to the localized string from AppResources.
		//    ApplicationBarIconButton appBarButton = new ApplicationBarIconButton(new Uri("/Assets/AppBar/appbar.add.rest.png", UriKind.Relative));
		//    appBarButton.Text = AppResources.AppBarButtonText;
		//    ApplicationBar.Buttons.Add(appBarButton);

		//    // Create a new menu item with the localized string from AppResources.
		//    ApplicationBarMenuItem appBarMenuItem = new ApplicationBarMenuItem(AppResources.AppBarMenuItemText);
		//    ApplicationBar.MenuItems.Add(appBarMenuItem);
		//}
	
	}
}