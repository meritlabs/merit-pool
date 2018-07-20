import argparse

# Parse arguments
ap = argparse.ArgumentParser()
ap.add_argument("-f", "--filename", required=False, help="Filename(from where read the distribution data)",
                default="mempool_invites.csv")
args = vars(ap.parse_args())


def distribute_invites():
    pass


if __name__ == "__main__":
    distribute_invites()