terraform {
  required_version = ">= 1.10"

  backend "s3" {
    bucket       = "restaurant-terraform-state-746430304139"
    key          = "restaurant/terraform.tfstate"
    region       = "us-east-2"
    use_lockfile = true
    encrypt      = true
  }
}
