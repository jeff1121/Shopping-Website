# Copilot Instructions

ASP.NET Web Forms (.NET Framework 4.7.2, C#) shopping site backed by SQL Server via ADO.NET. Single project: `Website/Website.csproj` in `Website.sln`.

## Build / run / test

- Windows + Visual Studio (2019+): open `Website.sln`, restore NuGet, build (`Ctrl+Shift+B`), run with IIS Express (`F5`, `https://localhost:44337/`). CLI equivalent: `nuget restore Website.sln` then `msbuild Website.sln`.
- There are no tests, linters, or CI.
- **The code does not compile as committed.** Every code-behind that talks to the DB contains the literal placeholder `new SqlConnection(<enter your database connection>)`, and `Web.config`'s `cmpConnectionString` is `"Add connection string here"`. Both must be filled in locally — never commit real connection strings.
- `iTextSharp` is referenced via a machine-specific `HintPath` (`..\..\..\..\Files\OrderInvoice\itextsharp.dll`) in `Website.csproj`, not NuGet (`packages.config` only has the Roslyn CodeDom provider). `profile.aspx.cs` uses it for the PDF order invoice.
- `Website.csproj` references `Properties\AssemblyInfo.cs` and `css\style.css`, but neither file is in the repo (missing `AssemblyInfo.cs` breaks the build). The repo has no `.gitignore`; `Website/obj/Debug/*` cache files are tracked. Some tracked product images (`apple.png`, `appol.png`, `img2.png`, `products/human99/laptop.png`) are not listed as `<Content Include>` in `Website.csproj`.
- Full setup steps, a schema script that matches the code, and troubleshooting live in `QuickStart.md`; `README.md` is the zh-TW project overview.

## Conventions

- Docs (including README), code comments, commit messages and PR titles/descriptions are all written in Traditional Chinese (zh-TW); only code identifiers stay in English. Domain terms are defined in `CONTEXT.md`, architecture decisions in `docs/adr/`, and the CI/CD + Azure deployment plan in `Plan.md`. Every class, method and field in code-behind has a zh-TW `/// <summary>`; every `.aspx` has a `<%-- --%>` purpose comment on line 2. Designer files also carry zh-TW field comments in the form `型別「ID」：用途` — Visual Studio regeneration reverts them to English, so restore them before committing. Keep this when adding code.
- The repo lives at `https://github.com/Jeff1121/Shopping-Website` (forked from `DarylFernandes99/Shopping-Website`).
- Source files are UTF-8 with BOM and CRLF line endings.

## Architecture

- Each page is a trio: `page.aspx` (markup), `page.aspx.cs` (code-behind, namespace `Website`, class = page name), `page.aspx.designer.cs` (control fields — keep in sync when adding/removing `runat="server"` controls). New files must also be registered in the old-style `Website.csproj` (`<Content Include>` / `<Compile Include>` with `DependentUpon`).
- There is **no master page**: the header/nav, logout button, cart icon and `countItems` badge are duplicated in every `.aspx`. Most `Page_Load` handlers repeat the same "if logged in, show profile/cart icons and set the badge from `Session["count"]`" block; notable exceptions/bugs include `login.aspx.cs` (no header-state work), `profile.aspx.cs` (reads `Session["count1"]`), and `sellerProfile.aspx` (uses plain anchor icons in markup). Shared UI/nav changes must be applied to every page.
- Styling: `Website/css/style.css` (linked from every page but missing from the repo) plus per-page inline `<style>` blocks.
- Two data-access styles coexist:
  - Declarative `asp:SqlDataSource` in markup using `<%$ ConnectionStrings:cmpConnectionString %>` (product list/search/sort/category in `index.aspx`, `categories.aspx`, order history and seller product edit/delete in `profile.aspx`). `index.aspx.cs` swaps the DataList's source among `SqlDataSource1..5`; `categories.aspx` links to `index.aspx?category=<name>`.
  - Imperative `SqlConnection`/`SqlCommand`/`SqlDataAdapter` in code-behind via a per-page `con` field; a mix of parameterized and string-concatenated SQL.

### Session state (implicit cross-page contract)

- `Session["user"]` – the login text (username **or** email); queries resolve it with `WHERE username=@x OR email=@x`. Logout sets it to `null`.
- `Session["uname"]` – the user's full name (`uname`), which is the actual PK/FK stored in cart, order and product rows.
- `Session["count"]` – the cart as a `DataTable` (columns `sno, pimage, pname, price, quantity, total, uname`); its row count drives the cart badge. The `uname` column is semantically inconsistent: add-to-cart stores the seller name from `violet_products.uname`, while login restore reads `violet_cart.uname` (buyer name). Rebuilt from `violet_cart` at login (`login.aspx.cs`). Note `profile.aspx.cs` reads `Session["count1"]`.
- `Session["addproduct"]` – `"true"` flag set by Add-to-Cart in `index.aspx.cs` before redirecting to `cart.aspx?id=<pname>&quantity=<n>`; `cart.aspx` only adds an item when the flag is set, then resets it.
- `Session["oldQuantity"]` – previous quantity while editing a cart row, used to rebalance stock.

### Cart / stock / order flow

Stock in `violet_products` is decremented on add-to-cart (`index.aspx.cs`), rebalanced on quantity edit and restored on delete (`cart.aspx.cs`). Cart rows live in both the session `DataTable` and `violet_cart` (with `sno` renumbered on delete). Checkout (`checkout.aspx.cs`) inserts rows into `violet_order` and deletes the user's `violet_cart` rows. Seller product images are saved to `img/products/<Session["user"]>/` and stored as relative paths in `pimage`.

## Database schema caveats

`DbSql.sql` is out of date relative to the code (and has a `CREATE DATABSE` typo). The code actually expects:

- `violet_user_login` (positional insert in `register.aspx.cs`, 14 columns): `uname, email, username, password, phone, dob, country, state, city, gender, address, secq, seca, uid`. Roles are encoded in `uid`: `generateUID()` assigns 1–4998 (customer); `uid > 5000` means seller (checked in `profile`/`sellerProfile`) and is never assigned by code.
- `violet_products`: `pname, price, pimage, category, uname, keywords` plus `stock` — the seller link column is `uname`, not `sname`. `addProducts.aspx.cs` inserts only the first 6 positionally, so it fails if `stock` exists.
- `violet_cart` (positional insert): `uname, sno, pimage, pname, price, quantity, total, sname`.
- `violet_order` (positional insert): `uname, pname, orderID, orderDate, quantity, total`.
- `violet_categories` (`name`, `cimage`) — not created by the script; names must match the hard-coded list in `addProducts.aspx.cs`.
- `violet_contact` (positional insert): `uname, email, message`.
- Seller pages (`sellerRegister`, `sellerSignIn`, `sellerProfile`) read/write `violet_user_login`; `violet_seller_login` is unused by code. `sellerRegister.aspx.cs` inserts only 13 values (no `uid`).

Many inserts use positional `INSERT ... VALUES(...)`, so column order matters. When changing the schema, update `DbSql.sql`, the schema script in `QuickStart.md` and the README schema section together.
