# frozen_string_literal: true

require 'rest-client'

COMMUNITY_FORUM_API_BASE = 'https://community.revolutionarygamesstudio.com/'

# Max number of patrons
DISCOURSE_QUERY_LIMIT = 10_000

# Module with helper function to do patreon operations
module CommunityForumGroups
  def self.headers
    { 'Api-Key' => ENV['COMMUNITY_DISCOURSE_API_KEY'],
      'Api-Username' => 'system',
      content_type: :json }
  end

  def self.query_users_in_group(group)
    url = URI.join(COMMUNITY_FORUM_API_BASE,
                   "/groups/#{group}/members.json").to_s
    # TODO: does this need to be added somehow to make sure we get all the members?
    # + "?offset=0&limit=#{DISCOURSE_QUERY_LIMIT}"

    response = RestClient.get(url, CommunityForumGroups.headers)

    data = JSON.parse(response.body)

    [data['members'], data['owners']]
  end

  # TODO: is this really, really slow?
  def self.find_user_by_email(email)
    url = URI.join(COMMUNITY_FORUM_API_BASE,
                   '/admin/users/list/all.json').to_s + "?email=#{email}"
    response = RestClient.get(url, CommunityForumGroups.headers)

    JSON.parse(response.body).first
  end

  def self.get_group_id(group)
    url = URI.join(COMMUNITY_FORUM_API_BASE,
                   "/groups/#{group}.json").to_s
    response = RestClient.get(url, CommunityForumGroups.headers)

    JSON.parse(response.body)['group']['id']
  end

  def self.prepapare_group_url_and_payload(group, usernames)
    id = get_group_id group

    url = URI.join(COMMUNITY_FORUM_API_BASE,
                   "/groups/#{id}/members.json").to_s

    payload = { usernames: usernames.join(',') }

    [url, payload]
  end

  def self.add_group_members(group, usernames)
    url, payload = prepapare_group_url_and_payload group, usernames

    RestClient.put(url, payload.to_json, CommunityForumGroups.headers)
  end

  def self.remove_group_members(group, usernames)
    url, payload = prepapare_group_url_and_payload group, usernames

    RestClient.delete(url, payload.to_json, CommunityForumGroups.headers)
  end
end
