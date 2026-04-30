import os
import shutil
import msvcrt
import traceback

def main():
	# Current working directory = mod root
	root_path = os.getcwd()
	root_name = os.path.basename(root_path)
	target_name = f"x{root_name}"

	print(f"Root detected: {root_name}")
	print(f"Path: {root_path}")

	# Target path: ../../../Mods/<root_name>
	target_base = os.path.abspath(os.path.join(root_path, "..", "..", "..", "Mods"))
	target_path = os.path.join(target_base, target_name)

	print(f"Target Mods folder: {target_base}")
	print(f"Final target path: {target_path}")

	print("Press any key to continue...")
	msvcrt.getch()

	# Safety check
	if not os.path.isdir(target_base):
		print("ERROR: Mods directory does not exist!")
		msvcrt.getch()
		return

	# Ignore function
	def ignore_filter(dir, contents):
		ignored = []

		for item in contents:
			# Ignore Source folder
			if item == "Source":
				ignored.append(item)

			# Ignore .git folder anywhere
			elif item == ".git":
				ignored.append(item)

			# Ignore .gitignore only in root
			elif item == ".gitignore":
				ignored.append(item)

			# Ignore all .py files
			elif item.endswith(".py"):
				ignored.append(item)

		return ignored

	if os.path.exists(target_path):
		print("Target exists, removing...")
		shutil.rmtree(target_path)

	print("Copying files...")
	shutil.copytree(
		root_path,
		target_path,
		ignore=ignore_filter
	)

	print("Done!")

if __name__ == "__main__":
	try:
		main()
	except Exception:
		traceback.print_exc()
		print("\nPress Enter to exit...")
		msvcrt.getch()